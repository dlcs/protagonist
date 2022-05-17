using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Collections;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using IIIF;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DLCS.Repository.Assets
{
    public class ThumbReorganiser : IThumbReorganiser
    {
        private static readonly Regex ExistingThumbsRegex =
            new(@".*\/full\/(\d+,\d+)\/.*", RegexOptions.Compiled);

        private readonly IBucketReader bucketReader;
        private readonly IBucketWriter bucketWriter;
        private readonly ILogger<ThumbReorganiser> logger;
        private readonly IAssetRepository assetRepository;
        private readonly IThumbnailPolicyRepository thumbnailPolicyRepository;
        private readonly IBucketKeyGenerator bucketKeyGenerator;
        private readonly AsyncKeyedLock asyncLocker = new();
        private static readonly Regex BoundedThumbRegex = new("^[0-9]+.jpg$");

        public ThumbReorganiser(
            IBucketReader bucketReader,
            IBucketWriter bucketWriter,
            ILogger<ThumbReorganiser> logger,
            IAssetRepository assetRepository,
            IThumbnailPolicyRepository thumbnailPolicyRepository,
            IBucketKeyGenerator bucketKeyGenerator)
        {
            this.bucketReader = bucketReader;
            this.bucketWriter = bucketWriter;
            this.logger = logger;
            this.assetRepository = assetRepository;
            this.thumbnailPolicyRepository = thumbnailPolicyRepository;
            this.bucketKeyGenerator = bucketKeyGenerator;
        }
        
        public async Task<ReorganiseResult> EnsureNewLayout(AssetId assetId)
        {
            // Create lock on assetId unique value (bucket + target key)
            using var processLock = await asyncLocker.LockAsync(assetId.ToString());
            
            var rootKey = bucketKeyGenerator.GetThumbsRoot(assetId);
            var keysInTargetBucket = await bucketReader.GetMatchingKeys(rootKey);
            if (HasCurrentLayout(assetId, keysInTargetBucket))
            {
                logger.LogDebug("{RootKey} has expected current layout", rootKey);
                return ReorganiseResult.HasExpectedLayout;
            }

            // under full/ we will find some sizes, but not the largest.
            // the largest is at low.jpg in the "root".
            // trouble is we do not know how big it is!
            // we'll need to fetch the image dimensions from the database, the Thumbnail policy the image was created with, and compute the sizes.
            // Then sanity check them against the known sizes.
            var asset = await assetRepository.GetAsset(assetId);

            // 404 Not Found Asset
            if (asset == null)
            {
                return ReorganiseResult.AssetNotFound;
            }

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

            var existingSizes = GetExistingSizesList(thumbnailSizes, keysInTargetBucket);

            // All the thumbnail jpgs will already exist and need copied up to root
            await CreateThumbnails(assetId, boundingSquares, thumbnailSizes, existingSizes);

            // Create sizes json file last, as this dictates whether this process will be attempted again
            await CreateSizesJson(assetId, thumbnailSizes);

            // Clean up legacy format from before /open /auth paths
            await CleanupRootConfinedSquareThumbs(rootKey, keysInTargetBucket);

            return ReorganiseResult.Reorganised;
        }

        private bool HasCurrentLayout(AssetId assetId, string[] keysInTargetBucket)
        {
            var thumbsSizesJsonKey = bucketKeyGenerator.GetThumbsSizesJsonKey(assetId);
            return keysInTargetBucket.Contains(thumbsSizesJsonKey.Key);
        }

        private static Size GetMaxAvailableThumb(Asset asset, ThumbnailPolicy policy)
        {
            var _ = asset.GetAvailableThumbSizes(policy, out var maxDimensions);
            return Size.Square(maxDimensions.maxBoundedSize);
        }

        private static List<Size> GetExistingSizesList(ThumbnailSizes thumbnailSizes, string[] keysInTargetBucket)
        {
            var existingSizes = new List<Size>(thumbnailSizes.Count);
            foreach (var keyInBucket in keysInTargetBucket)
            {
                var match = ExistingThumbsRegex.Match(keyInBucket);
                if (match.Success)
                {
                    existingSizes.Add(Size.FromString(match.Groups[1].Value));
                }
            }

            return existingSizes;
        }

        private async Task CreateThumbnails(AssetId assetId, List<int> boundingSquares, ThumbnailSizes thumbnailSizes,
            List<Size> existingSizes)
        {
            var copyTasks = new List<Task>(thumbnailSizes.Count);

            // low.jpg becomes the first in this list
            var largestSize = boundingSquares[0];
            var largestIsOpen = thumbnailSizes.Auth.IsNullOrEmpty();

            copyTasks.Add(bucketWriter.CopyObject(
                bucketKeyGenerator.GetLargestThumbsKey(assetId),
                bucketKeyGenerator.GetThumbnailKey(assetId, largestSize, largestIsOpen)));

            copyTasks.AddRange(ProcessThumbBatch(assetId, false, thumbnailSizes.Auth, largestSize, existingSizes));
            copyTasks.AddRange(ProcessThumbBatch(assetId, true, thumbnailSizes.Open, largestSize, existingSizes));

            await Task.WhenAll(copyTasks);
        }

        private IEnumerable<Task> ProcessThumbBatch(AssetId assetId, bool isOpen, IEnumerable<int[]> thumbnailSizes,
            int largestSize, List<Size> existingSizes)
        {
            foreach (var wh in thumbnailSizes)
            {
                var currentSize = Size.FromArray(wh);
                var maxDimension = currentSize.MaxDimension;
                if (maxDimension == largestSize) continue;

                // NOTE: Due to legacy issues with rounding calculations between .net and Python, there may be a slight
                // difference between the keys in S3 and the desired size calculated here. To avoid any bugs, look at
                // existing keys in s3 to decide what key to copy, rather than what calculation says we should copy.
                var sizeCandidates = existingSizes.Where(s => s.MaxDimension == maxDimension).ToList();
                if (sizeCandidates.IsNullOrEmpty())
                {
                    logger.LogWarning("Unable to find thumb with max dimension {MaxDimension} for asset '{AssetId}'",
                        maxDimension, assetId);
                    continue;
                }

                // NOTE: In rare occasions there may be multiple thumbs with the same MaxDimension (due to historical
                // rounding issue). In that case look for an exact match.
                var toCopy = sizeCandidates.Count == 1
                    ? sizeCandidates[0]
                    : sizeCandidates.SingleOrDefault(
                        s => s.Width == currentSize.Width && s.Height == currentSize.Height);

                if (toCopy == null)
                {
                    logger.LogWarning("Unable to find thumb with max dimension {MaxDimension} for rootKey '{AssetId}'",
                        maxDimension, assetId);
                    continue;
                }

                yield return bucketWriter.CopyObject(
                    bucketKeyGenerator.GetLegacyThumbnailKey(assetId, toCopy.Width, toCopy.Height),
                    bucketKeyGenerator.GetThumbnailKey(assetId, maxDimension, isOpen)
                );
            }
        }

        private async Task CreateSizesJson(AssetId assetId, ThumbnailSizes thumbnailSizes)
        {
            var sizesDest = bucketKeyGenerator.GetThumbsSizesJsonKey(assetId);
            await bucketWriter.WriteToBucket(sizesDest, JsonConvert.SerializeObject(thumbnailSizes),
                "application/json");
        }

        private async Task CleanupRootConfinedSquareThumbs(ObjectInBucket rootKey, string[] s3ObjectKeys)
        {
            // This is an interim method to clean up the first implementation of /thumbs/ handling
            // which created all thumbs at root and sizes.json, rather than s.json
            // We output s.json now. Previously this was sizes.json
            const string oldSizesJsonKey = "sizes.json";

            if (s3ObjectKeys.IsNullOrEmpty()) return;

            List<ObjectInBucket> toDelete = new(s3ObjectKeys.Length);

            foreach (var key in s3ObjectKeys)
            {
                string item = key.Replace(rootKey.Key, string.Empty);
                if (BoundedThumbRegex.IsMatch(item) || item == oldSizesJsonKey)
                {
                    logger.LogDebug("Deleting legacy confined-thumb object: '{Key}'", key);
                    toDelete.Add(new ObjectInBucket(rootKey.Bucket, key));
                }
            }

            if (toDelete.Count > 0)
            {
                await bucketWriter.DeleteFromBucket(toDelete.ToArray());
            }
        }
    }

    public enum ReorganiseResult
    {
        /// <summary>
        /// Default
        /// </summary>
        Unknown,
        
        /// <summary>
        /// Asset with specified key was not found
        /// </summary>
        AssetNotFound,
        
        /// <summary>
        /// Key already has expected layout
        /// </summary>
        HasExpectedLayout,
        
        /// <summary>
        /// Layout was successfully reorganised.
        /// </summary>
        Reorganised
    }
}
