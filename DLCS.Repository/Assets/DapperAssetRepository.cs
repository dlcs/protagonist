using System.Threading.Tasks;
using Dapper;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Assets
{
    /// <summary>
    /// Implementation of <see cref="IAssetRepository"/> using Dapper for data access.
    /// </summary>
    public class DapperAssetRepository : IAssetRepository
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<DapperAssetRepository> logger;

        public DapperAssetRepository(IConfiguration configuration, ILogger<DapperAssetRepository> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<Asset?> GetAsset(string id)
        {
            // TODO - cache
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QuerySingleOrDefaultAsync<Asset>(AssetSql, new {Id = id});
        }

        public Task<Asset?> GetAsset(AssetId id)
            => GetAsset(id.ToString());

        private const string AssetSql = @"
SELECT ""Id"", ""Customer"", ""Space"", ""Created"", ""Origin"", ""Tags"", ""Roles"", 
""PreservedUri"", ""Reference1"", ""Reference2"", ""Reference3"", ""MaxUnauthorised"", 
""NumberReference1"", ""NumberReference2"", ""NumberReference3"", ""Width"", 
""Height"", ""Error"", ""Batch"", ""Finished"", ""Ingesting"", ""ImageOptimisationPolicy"", 
""ThumbnailPolicy"", ""Family"", ""MediaType"", ""Duration""
  FROM public.""Images""
  WHERE ""Id""=@Id;";
    }
}
