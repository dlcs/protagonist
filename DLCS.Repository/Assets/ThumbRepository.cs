using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository.Settings;
using IIIF.ImageApi;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DLCS.Repository.Assets
{
    public class ThumbRepository : IThumbRepository
    {
        private readonly IAppCache appCache;
        private readonly ILogger<ThumbRepository> logger;
        private readonly IBucketReader bucketReader;
        private readonly IAssetRepository assetRepository;
        private readonly IOptionsMonitor<ThumbsSettings> settings;
        private readonly IConfiguration configuration;

        public ThumbRepository(
            IAppCache appCache,
            IConfiguration configuration,
            ILogger<ThumbRepository> logger,
            IBucketReader bucketReader,
            IAssetRepository assetRepository,
            IOptionsMonitor<ThumbsSettings> settings)
        {
            this.configuration = configuration;    
            this.logger = logger;
            this.bucketReader = bucketReader;
            this.assetRepository = assetRepository;
            this.settings = settings;
            this.appCache = appCache;
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
                Key = GetKeyRoot(customerId, spaceId, imageRequest) + $"{longestEdge}.jpg"
            };
        }

        public async Task<List<int[]>> GetSizes(int customerId, int spaceId, ImageRequest imageRequest)
        {
            await EnsureNewLayout(customerId, spaceId, imageRequest);

            ObjectInBucket sizesList = new ObjectInBucket
            {
                Bucket = settings.CurrentValue.ThumbsBucket,
                Key = GetKeyRoot(customerId, spaceId, imageRequest) + "sizes.json"
            };
            var stream = new MemoryStream();
            await bucketReader.WriteObjectFromBucket(sizesList, stream);
            var serializer = new JsonSerializer();
            stream.Position = 0;
            using var sr = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(sr);
            return serializer.Deserialize<List<int[]>>(jsonTextReader);
        }
        
        public async Task<ThumbnailPolicy> GetThumbnailPolicy(string thumbnailPolicyId)
        {
            var thumbnailPolicies = await GetThumbnailPolicies();
            return thumbnailPolicies.SingleOrDefault(p => p.Id == thumbnailPolicyId);
        }

        private async Task<List<ThumbnailPolicy>> GetThumbnailPolicies()
        {
            const string key = "ThumbRepository_ThumbnailPolicies";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                logger.LogInformation("refreshing ThumbnailPolicies from database");
                await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
                var thumbnailPolicies = await connection.QueryAsync<ThumbnailPolicy>(
                    "SELECT \"Id\", \"Name\", \"Sizes\" FROM \"ThumbnailPolicies\"");
                return thumbnailPolicies.ToList();
            });
        }

        private string GetKeyRoot(int customerId, int spaceId, ImageRequest imageRequest)
        {
            return $"{customerId}/{spaceId}/{imageRequest.Identifier}/";
        }

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
            
            return new ThumbReorganiser(rootKey, bucketReader, logger, assetRepository, this)
                .EnsureNewLayout();
        }
    }
}
