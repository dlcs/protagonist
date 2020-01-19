using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using IIIF.ImageApi;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;

namespace DLCS.Repository.Assets
{
    public class ThumbRepository : IThumbRepository
    {
        private readonly IMemoryCache memoryCache;
        private readonly IConfiguration configuration;
        private readonly ILogger<ThumbRepository> logger;
        private readonly bool ensureNewLayout;
        private readonly IBucketReader bucketReader;
        private readonly string thumbsBucket;
        private readonly IAssetRepository assetRepository;

        public ThumbRepository(
            IMemoryCache memoryCache,
            IConfiguration configuration,
            ILogger<ThumbRepository> logger,
            IBucketReader bucketReader,
            IAssetRepository assetRepository)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.bucketReader = bucketReader;
            this.assetRepository = assetRepository;
            this.memoryCache = memoryCache;

            // application config? Or Constructor params..?
            ensureNewLayout = configuration.GetValue("EnsureNewThumbnailLayout", false);
            thumbsBucket = configuration.GetValue<string>("ThumbsBucket");
        }

        public async Task<ObjectInBucket> GetThumbLocation(int customerId, int spaceId, ImageRequest imageRequest)
        {
            EnsureNewLayout(customerId, spaceId, imageRequest);
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
            }
            return new ObjectInBucket
            {
                Bucket = thumbsBucket,
                Key = GetKeyRoot(customerId, spaceId, imageRequest) + $"{longestEdge}.jpg"
            };
        }


        public async Task<List<int[]>> GetSizes(int customerId, int spaceId, ImageRequest imageRequest)
        {
            EnsureNewLayout(customerId, spaceId, imageRequest);

            ObjectInBucket sizesList = new ObjectInBucket
            {
                Bucket = thumbsBucket,
                Key = GetKeyRoot(customerId, spaceId, imageRequest) + "sizes.json"
            };
            var stream = new MemoryStream();
            await bucketReader.WriteObjectFromBucket(sizesList, stream);
            var serializer = new JsonSerializer();
            stream.Position = 0;
            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return (List<int[]>) serializer.Deserialize(jsonTextReader, typeof(List<int[]>));
            }
        }

        private List<ThumbnailPolicy> GetThumbnailPolicies()
        {
            const string key = "ThumbRepository_ThumbnailPolicies";
            return memoryCache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                logger.LogInformation("refreshing ThumbnailPolicies from database"); 
                using (var connection = new NpgsqlConnection(configuration.GetConnectionString("PostgreSQLConnection")))
                {
                    connection.Open();
                    return connection.Query<ThumbnailPolicy>(
                        "SELECT \"Id\", \"Name\", \"Sizes\" FROM \"ThumbnailPolicies\"")
                        .ToList();
                }
            });

        }

        public ThumbnailPolicy GeThumbnailPolicy(string thumbnailPolicyId)
        {
            return GetThumbnailPolicies().SingleOrDefault(p => p.Id == thumbnailPolicyId);
        }

        private string GetKeyRoot(int customerId, int spaceId, ImageRequest imageRequest)
        {
            return $"{customerId}/{spaceId}/{imageRequest.Identifier}/";
        }


        private void EnsureNewLayout(in int customerId, in int spaceId, ImageRequest imageRequest)
        {
            if (!ensureNewLayout)
            {
                return;
            }

            var rootKey = new ObjectInBucket
            {
                Bucket = thumbsBucket,
                Key = GetKeyRoot(customerId, spaceId, imageRequest)
            };
            var thumbReorganiser = new ThumbReorganiser(rootKey, bucketReader, logger, assetRepository, this);
            thumbReorganiser.EnsureNewLayout();
        }
    }
}
