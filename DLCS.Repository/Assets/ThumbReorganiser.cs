using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Threading;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository.Settings;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DLCS.Repository.Assets
{
    public class ThumbReorganiser : IThumbReorganiser
    {
        private readonly IBucketReader bucketReader;
        private readonly ILogger<ThumbRepository> logger;
        private readonly IAssetRepository assetRepository;
        private readonly IThumbnailPolicyRepository thumbnailPolicyRepository;
        private readonly AsyncKeyedLock asyncLocker = new AsyncKeyedLock();

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
            
            if (await HasCurrentLayout(rootKey))
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
            var realSize = new Size{ Width = asset.Width, Height = asset.Height };
            var boundingSquares = policy.SizeList.OrderByDescending(i => i).ToList();
            var expectedSizes = new List<Size>(boundingSquares.Count);
            foreach (int boundingSquare in boundingSquares)
            {
                expectedSizes.Add(Size.Confine(boundingSquare, realSize));
            }

            // All the thumbnail jpgs will already exist and need copied up to root
            await CreateThumbnails(rootKey, boundingSquares, expectedSizes);

            // Create sizes.json last, as this dictates whether this process will be attempted again
            await CreateSizesJson(rootKey, expectedSizes);
        }

        private async Task<bool> HasCurrentLayout(ObjectInBucket rootKey)
        {
            var keys = await bucketReader.GetMatchingKeys(rootKey);
            return keys.Contains($"{rootKey.Key}{ThumbsSettings.Constants.SizesJsonKey}");
        }

        private async Task CreateThumbnails(ObjectInBucket rootKey, List<int> boundingSquares, List<Size> expectedSizes)
        {
            var copyTasks = new List<Task>(expectedSizes.Count);
            
            // low.jpg becomes the first in this list
            copyTasks.Add(bucketReader.CopyWithinBucket(rootKey.Bucket,
                $"{rootKey.Key}low.jpg",
                $"{rootKey.Key}{boundingSquares[0]}.jpg"));

            int n = 1;
            foreach (Size size in expectedSizes.Skip(1))
            {
                copyTasks.Add(bucketReader.CopyWithinBucket(rootKey.Bucket,
                    $"{rootKey.Key}full/{size.Width},{size.Height}/0/default.jpg",
                    $"{rootKey.Key}{boundingSquares[n++]}.jpg"));
            }

            await Task.WhenAll(copyTasks);
        }

        private async Task CreateSizesJson(ObjectInBucket rootKey, List<Size> expectedSizes)
        {
            List<int[]> sizesJson = expectedSizes
                .Select(s => new int[] {s.Width, s.Height})
                .ToList();
            var sizesDest = rootKey.Clone();
            sizesDest.Key += "sizes.json";
            await bucketReader.WriteToBucket(sizesDest, JsonConvert.SerializeObject(sizesJson), "application/json");
        }

        public void DeleteOldLayout()
        {
            throw new NotImplementedException("Not yet! Need to be sure of all the others first!");
        }
    }
}
