using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository.Settings;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DLCS.Repository.Assets
{
    public class ThumbRepository : IThumbRepository
    {
        private readonly ILogger<ThumbRepository> logger;
        private readonly IBucketReader bucketReader;
        private readonly IOptionsMonitor<ThumbsSettings> settings;
        private readonly IThumbReorganiser thumbReorganiser;

        public ThumbRepository(
            ILogger<ThumbRepository> logger,
            IBucketReader bucketReader,
            IOptionsMonitor<ThumbsSettings> settings,
            IThumbReorganiser thumbReorganiser)
        {
            this.logger = logger;
            this.bucketReader = bucketReader;
            this.settings = settings;
            this.thumbReorganiser = thumbReorganiser;
        }

        public async Task<ObjectInBucket> GetThumbLocation(int customerId, int spaceId, ImageRequest imageRequest)
        {
            await EnsureNewLayout(customerId, spaceId, imageRequest);
            int longestEdge = 0;
            if (imageRequest.Size.Width > 0 && imageRequest.Size.Height > 0)
            {
                // We don't actually need to check imageRequest.Size.Confined (!w,h) because same logic applies...
                longestEdge = Math.Max(imageRequest.Size.Width, imageRequest.Size.Height);
            }
            else
            {
                // we need to know the sizes of things...
                var sizes = await GetSizes(customerId, spaceId, imageRequest);
                if (imageRequest.Size.Width > 0)
                {
                    foreach (var size in sizes)
                    {
                        if (size[0] == imageRequest.Size.Width)
                        {
                            longestEdge = Math.Max(size[0], size[1]);
                            break;
                        }
                    }
                }
                if (imageRequest.Size.Height > 0)
                {
                    foreach (var size in sizes)
                    {
                        if (size[1] == imageRequest.Size.Height)
                        {
                            longestEdge = Math.Max(size[0], size[1]);
                            break;
                        }
                    }
                }

                if (imageRequest.Size.Max)
                {
                    longestEdge = Math.Max(sizes[0][0], sizes[0][1]);
                }
            }
            return new ObjectInBucket
            {
                Bucket = settings.CurrentValue.ThumbsBucket,
                Key = $"{GetKeyRoot(customerId, spaceId, imageRequest)}open/{longestEdge}.jpg"
            };
        }

        public async Task<List<int[]>> GetSizes(int customerId, int spaceId, ImageRequest imageRequest)
        {
            await EnsureNewLayout(customerId, spaceId, imageRequest);

            ObjectInBucket sizesList = new ObjectInBucket
            {
                Bucket = settings.CurrentValue.ThumbsBucket,
                Key = string.Concat(GetKeyRoot(customerId, spaceId, imageRequest), ThumbsSettings.Constants.SizesJsonKey)
            };
            
            await using var stream = await bucketReader.GetObjectFromBucket(sizesList);
            if (stream == null)
            {
                logger.LogError("Could not find sizes file for request '{OriginalPath}'", imageRequest.OriginalPath);
                return new List<int[]>();
            }
            
            var serializer = new JsonSerializer();
            using var sr = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(sr);
            var thumbnailSizes = serializer.Deserialize<ThumbnailSizes>(jsonTextReader);
            return thumbnailSizes.Open;
        }

        private string GetKeyRoot(int customerId, int spaceId, ImageRequest imageRequest) 
            => $"{customerId}/{spaceId}/{imageRequest.Identifier}/";

        private Task EnsureNewLayout(int customerId, int spaceId, ImageRequest imageRequest)
        {
            var currentSettings = this.settings.CurrentValue;
            if (!currentSettings.EnsureNewThumbnailLayout)
            {
                return Task.CompletedTask;
            }

            var rootKey = new ObjectInBucket
            {
                Bucket = currentSettings.ThumbsBucket,
                Key = GetKeyRoot(customerId, spaceId, imageRequest)
            };

            return thumbReorganiser.EnsureNewLayout(rootKey);
        }
    }

    public class ThumbnailSizes
    {
        [JsonProperty("o")]
        public List<int[]> Open { get; }
            
        [JsonProperty("a")]
        public List<int[]> Auth { get; }

        [JsonConstructor]
        public ThumbnailSizes(List<int[]> open, List<int[]> auth)
        {
            Open = open;
            Auth = auth;
        }
        
        public ThumbnailSizes(int sizesCount)
        {
            Open = new List<int[]>(sizesCount);
            Auth = new List<int[]>(sizesCount);
        }

        public void AddAuth(int width, int height)
            => Auth.Add(new[] {width, height});
            
        public void AddOpen(int width, int height)
            => Open.Add(new[] {width, height});
    }
}
