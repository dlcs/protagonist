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
            Type = firstAdjunct.Type,
            // Adjuncts don't have their own roles, they inherit those of the parent Asset. Metadata is used to
            // evaluate appointment roles
            Asset = new Asset
            {
                Roles = DapperColumnMapping.SplitDelimited(firstAdjunct.Roles),
                Tags = DapperColumnMapping.SplitDelimited(firstAdjunct.Tags),
                Reference1 = firstAdjunct.Reference1,
                Reference2 = firstAdjunct.Reference2,
                Reference3 = firstAdjunct.Reference3,
                NumberReference1 = firstAdjunct.NumberReference1,
                NumberReference2 = firstAdjunct.NumberReference2,
                NumberReference3 = firstAdjunct.NumberReference3,
                Manifests = (firstAdjunct.Manifests as string[])?.ToList(),
            }
        };
    }

    private const string AdjunctSql =
        """
        SELECT "Adjuncts"."Id", "Adjuncts"."AssetId", "Adjuncts"."Origin", "Adjuncts"."IIIFLink",
        "Adjuncts"."MediaType", "Adjuncts"."Type", "Images"."Roles", "Images"."Tags", "Images"."Reference1",
        "Images"."Reference2", "Images"."Reference3", "Images"."NumberReference1", "Images"."NumberReference2",
        "Images"."NumberReference3", "Images"."Manifests"
        FROM "Adjuncts"
        INNER JOIN "Images" ON "Adjuncts"."AssetId" = "Images"."Id"
        WHERE "Adjuncts"."Id" = @Id AND "Adjuncts"."AssetId" = @AssetId
        """;
}
