using System;
using System.Threading.Tasks;
using Dapper;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Settings;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets
{
    /// <summary>
    /// Implementation of <see cref="IAssetRepository"/> using Dapper for data access.
    /// </summary>
    public class DapperAssetRepository : IAssetRepository
    {
        private readonly IConfiguration configuration;
        private readonly CacheSettings cacheSettings;
        private readonly IAppCache appCache;
        private readonly ILogger<DapperAssetRepository> logger;

        public DapperAssetRepository(
            IConfiguration configuration, 
            IAppCache appCache,
            IOptions<CacheSettings> cacheOptions,
            ILogger<DapperAssetRepository> logger)
        {
            this.appCache = appCache;
            this.logger = logger;
            cacheSettings = cacheOptions.Value;
            this.configuration = configuration;
        }

        public async Task<Asset?> GetAsset(string id)
        {
            var key = $"asset:{id}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                logger.LogInformation("Refreshing assetCache from database {Asset}", id);
                await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return await connection.QuerySingleOrDefaultAsync<Asset>(AssetSql, new { Id = id });
            });
        }

        public Task<Asset?> GetAsset(AssetId id)
            => GetAsset(id.ToString());

        public async Task<ImageLocation> GetImageLocation(AssetId assetId)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QuerySingleOrDefaultAsync<ImageLocation>(ImageLocationSql,
                new { Id = assetId.ToString() });
        }

        private const string AssetSql = @"
SELECT ""Id"", ""Customer"", ""Space"", ""Created"", ""Origin"", ""Tags"", ""Roles"", 
""PreservedUri"", ""Reference1"", ""Reference2"", ""Reference3"", ""MaxUnauthorised"", 
""NumberReference1"", ""NumberReference2"", ""NumberReference3"", ""Width"", 
""Height"", ""Error"", ""Batch"", ""Finished"", ""Ingesting"", ""ImageOptimisationPolicy"", 
""ThumbnailPolicy"", ""Family"", ""MediaType"", ""Duration""
  FROM public.""Images""
  WHERE ""Id""=@Id;";

        private const string ImageLocationSql =
            "SELECT \"Id\", \"S3\", \"Nas\" FROM public.\"ImageLocation\" WHERE \"Id\"=@Id;";
    }
}
