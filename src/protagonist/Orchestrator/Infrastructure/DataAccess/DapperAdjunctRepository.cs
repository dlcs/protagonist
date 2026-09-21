using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Enum;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Microsoft.Extensions.Configuration;

namespace Orchestrator.Infrastructure.DataAccess;

public class DapperAdjunctRepository(
    IConfiguration configuration,
    AssetCachingHelper assetCachingHelper): IOrchestratorAdjunctRepository, IDapperConfigRepository
{
    public IConfiguration Configuration { get; } = configuration;
    
    public Task<Adjunct?> GetAdjunct(string adjunctId, AssetId assetId, bool noCache)
    {
        if (noCache)
        {
            assetCachingHelper.RemoveAdjunctFromCache(adjunctId, assetId);
        }
        
        return GetAdjunct(adjunctId, assetId);
    }
    
    private async Task<Adjunct?> GetAdjunct(string adjunctId, AssetId assetId)
    {
        return await assetCachingHelper.GetCachedAdjunct(adjunctId, assetId, GetAdjunctInternal);
    }

    private async Task<Adjunct?> GetAdjunctInternal(string adjunctId, AssetId assetId)
    {
        IEnumerable<dynamic> rawAdjunct =
            await this.QueryAsync(AdjunctSql, new { Id = adjunctId, AssetId = assetId.ToString() });
        var convertedRawAsset = rawAdjunct.ToList();
        if (convertedRawAsset.Count == 0)
        {
            return null;
        }

        var firstAdjunct = convertedRawAsset[0];
        return new Adjunct
        {
            Id = firstAdjunct.Id,
            AssetId = AssetId.FromString(firstAdjunct.AssetId),
            Origin = firstAdjunct.Origin,
            IIIFLink = ((string)firstAdjunct.IIIFLink).GetEnumFromString<IIIFLinkType>(),
            MediaType = firstAdjunct.MediaType,
            Type = firstAdjunct.Type
        };
    }

    private const string AdjunctSql =
        """
        SELECT "Id", "AssetId", "Origin", "IIIFLink", "MediaType", "Type"
        FROM "Adjuncts"
        WHERE "Id" = @Id AND "AssetId" = @AssetId
        """;

}
