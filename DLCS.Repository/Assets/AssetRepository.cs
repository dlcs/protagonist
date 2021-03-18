using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Assets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Assets
{

    public class AssetRepository : IAssetRepository
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<AssetRepository> logger;

        public AssetRepository(IConfiguration configuration, ILogger<AssetRepository> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<Asset?> GetAsset(string id)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QuerySingleOrDefaultAsync<Asset>(AssetSql, new {Id = id});
        }

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
