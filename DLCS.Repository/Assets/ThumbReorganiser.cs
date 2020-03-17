﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Threading;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using IIIF;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using thumbConsts =  DLCS.Repository.Settings.ThumbsSettings.Constants;

namespace DLCS.Repository.Assets
{
    public class ThumbReorganiser : IThumbReorganiser
    {
        private readonly IBucketReader bucketReader;
        private readonly ILogger<ThumbRepository> logger;
        private readonly IAssetRepository assetRepository;
        private readonly IThumbnailPolicyRepository thumbnailPolicyRepository;
        private readonly AsyncKeyedLock asyncLocker = new AsyncKeyedLock();
        private static Regex BoundedThumbRegex = new Regex("^[0-9]+.jpg$");

        public ThumbReorganiser(
            IBucketReader bucketReader,
            ILogger<ThumbRepository> logger,
            IAssetRepository assetRepository,
            IThumbnailPolicyRepository thumbnailPolicyRepository )
        {
            this.bucketReader = bucketReader;
            this.logger = logger;
            this.assetRepository = assetRepository;
            this.thumbnailPolicyRepository = thumbnailPolicyRepository;
        }

        public async Task EnsureNewLayout(ObjectInBucket rootKey)
        {
            // Create lock on rootKey unique value (bucket + target key)
            using var processLock = await asyncLocker.LockAsync(rootKey.ToString());
            
            var keysInTargetBucket = await bucketReader.GetMatchingKeys(rootKey);
            if (HasCurrentLayout(rootKey, keysInTargetBucket))
            {
                logger.LogDebug("{RootKey} has expected current layout", rootKey);
                return;
            }

            // under full/ we will find some sizes, but not the largest.
            // the largest is at low.jpg in the "root".
            // trouble is we do not know how big it is!
            // we'll need to fetch the image dimensions from the database, the Thumbnail policy the image was created with, and compute the sizes.
            // Then sanity check them against the known sizes.
            
            var asset = await assetRepository.GetAsset(rootKey.Key.TrimEnd('/'));
            var policy = await thumbnailPolicyRepository.GetThumbnailPolicy(asset.ThumbnailPolicy);

            var maxAvailableThumb = GetMaxAvailableThumb(asset, policy);

            var realSize = new Size(asset.Width, asset.Height);
            var boundingSquares = policy.SizeList.OrderByDescending(i => i).ToList();

            var thumbnailSizes = new ThumbnailSizes(boundingSquares.Count);
            foreach (int boundingSquare in boundingSquares)
            {
                var thumb = Size.Confine(boundingSquare, realSize);
                if (thumb.IsConfinedWithin(maxAvailableThumb))
                {
                    thumbnailSizes.AddOpen(thumb);
                }
                else
                {
                    thumbnailSizes.AddAuth(thumb);
                }
            }

            // All the thumbnail jpgs will already exist and need copied up to root
            await CreateThumbnails(rootKey, boundingSquares, thumbnailSizes);

            // Create sizes json file last, as this dictates whether this process will be attempted again
            await CreateSizesJson(rootKey, thumbnailSizes);

            // Clean up legacy format from before /open /auth paths
            await CleanupRootConfinedSquareThumbs(rootKey, keysInTargetBucket);
        }

        private static bool HasCurrentLayout(ObjectInBucket rootKey, string[] keysInTargetBucket) =>
            keysInTargetBucket.Contains($"{rootKey.Key}{thumbConsts.SizesJsonKey}");

        private static Size GetMaxAvailableThumb(Asset asset, ThumbnailPolicy policy)
        {
            var _ = asset.GetAvailableThumbSizes(policy, out var maxDimensions);
            return Size.Square(maxDimensions.maxBoundedSize);
        }

        private async Task CreateThumbnails(ObjectInBucket rootKey, List<int> boundingSquares, ThumbnailSizes thumbnailSizes)
        {
            var copyTasks = new List<Task>(thumbnailSizes.Count);
            
            // low.jpg becomes the first in this list
            var largestSize = boundingSquares[0];
            var largestSlug = thumbnailSizes.Auth.IsNullOrEmpty() ? thumbConsts.OpenSlug : thumbConsts.AuthorisedSlug;
            copyTasks.Add(bucketReader.CopyWithinBucket(rootKey.Bucket,
                $"{rootKey.Key}low.jpg",
                $"{rootKey.Key}{largestSlug}/{largestSize}.jpg"));
            
            copyTasks.AddRange(ProcessThumbBatch(rootKey, thumbnailSizes.Auth, thumbConsts.AuthorisedSlug, largestSize));
            copyTasks.AddRange(ProcessThumbBatch(rootKey, thumbnailSizes.Open, thumbConsts.OpenSlug, largestSize));
            
            await Task.WhenAll(copyTasks);
        }

        private IEnumerable<Task> ProcessThumbBatch(ObjectInBucket rootKey, IEnumerable<int[]> thumbnailSizes,
            string slug, int largestSize)
        {
            foreach (var wh in thumbnailSizes)
            {
                var size = Size.FromArray(wh);
                if (size.MaxDimension == largestSize) continue;

                yield return bucketReader.CopyWithinBucket(rootKey.Bucket,
                    $"{rootKey.Key}full/{size.Width},{size.Height}/0/default.jpg",
                    $"{rootKey.Key}{slug}/{size.MaxDimension}.jpg");
            }
        }

        private async Task CreateSizesJson(ObjectInBucket rootKey, ThumbnailSizes thumbnailSizes)
        {
            var sizesDest = rootKey.Clone();
            sizesDest.Key += thumbConsts.SizesJsonKey;
            await bucketReader.WriteToBucket(sizesDest, JsonConvert.SerializeObject(thumbnailSizes), "application/json");
        }

        private async Task CleanupRootConfinedSquareThumbs(ObjectInBucket rootKey, string[] s3ObjectKeys)
        {
            // This is an interim method to clean up the first implementation of /thumbs/ handling
            // which created all thumbs at root and sizes.json, rather than s.json
            // We output s.json now. Previously this was sizes.json
            const string oldSizesJsonKey = "sizes.json";
            
            if (s3ObjectKeys.IsNullOrEmpty()) return;
            
            List<ObjectInBucket> toDelete = new List<ObjectInBucket>(s3ObjectKeys.Length);
            
            foreach (var key in s3ObjectKeys)
            {
                string item = key.Replace(rootKey.Key, string.Empty);
                if (BoundedThumbRegex.IsMatch(item) || item == oldSizesJsonKey)
                {
                    logger.LogDebug($"Deleting legacy confined-thumb object: '{key}'");
                    toDelete.Add(new ObjectInBucket
                    {
                        Bucket = rootKey.Bucket,
                        Key = key
                    });
                }
            }

            if (toDelete.Count > 0)
            {
                await bucketReader.DeleteFromBucket(toDelete.ToArray());
            }
        }
        
        public void DeleteOldLayout()
        {
            throw new NotImplementedException("Not yet! Need to be sure of all the others first!");
        }
    }
}
