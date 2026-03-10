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

/// <summary>
/// Implementation of <see cref="IAssetRepository"/> using Dapper for data access.
/// </summary>
public class DapperAssetRepository(
    IConfiguration configuration,
    AssetCachingHelper assetCachingHelper)
    : IOrchestratorAssetRepository, IDapperConfigRepository
{
    public IConfiguration Configuration { get; } = configuration;

    public async Task<ImageLocation?> GetImageLocation(AssetId assetId)
        => await this.QuerySingleOrDefaultAsync<ImageLocation>(ImageLocationSql, new {Id = assetId.ToString()});

    public Task<Asset?> GetAsset(AssetId assetId, bool noCache)
    {
        if (noCache)
        {
            assetCachingHelper.RemoveAssetFromCache(assetId);
        }
        
        return GetAsset(assetId);
    }

    public Task<Adjunct?> GetAdjunct(string adjunctId, AssetId assetId, bool noCache)
    {
        if (noCache)
        {
            assetCachingHelper.RemoveAdjunctFromCache(adjunctId, assetId);
        }
        
        return GetAdjunct(adjunctId, assetId);
    }

    public async Task<Asset?> GetAsset(AssetId assetId)
    {
        var asset = await assetCachingHelper.GetCachedAsset(assetId, GetAssetInternal);
        return asset;
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
            // TODO: Add more if turns out it's needed, also add to SQL
        };
    }
    
    private async Task<Asset?> GetAssetInternal(AssetId assetId)
    {
        var id = assetId.ToString();
        IEnumerable<dynamic> rawAsset = await this.QueryAsync(AssetSql, new { Id = id });
        var convertedRawAsset = rawAsset.ToList();
        if (convertedRawAsset.Count == 0)
        {
            return null;
        }

        var firstAsset = convertedRawAsset[0];

        return new Asset
        {
            Batch = firstAsset.Batch,
            Created = firstAsset.Created,
            Customer = firstAsset.Customer,
            Duration = firstAsset.Duration,
            Error = firstAsset.Error,
            Family = (AssetFamily)firstAsset.Family.ToString()[0],
            Finished = firstAsset.Finished,
            Height = firstAsset.Height,
            Id = AssetId.FromString(firstAsset.Id),
            Ingesting = firstAsset.Ingesting,
            Origin = firstAsset.Origin,
            Reference1 = firstAsset.Reference1,
            Reference2 = firstAsset.Reference2,
            Reference3 = firstAsset.Reference3,
            Roles = firstAsset.Roles,
            Space = firstAsset.Space,
            Tags = firstAsset.Tags,
            Width = firstAsset.Width,
            MaxUnauthorised = firstAsset.MaxUnauthorised,
            MaxWidth = firstAsset.MaxWidth,
            OpenFullMax = firstAsset.OpenFullMax,
            MediaType = firstAsset.MediaType,
            NumberReference1 = firstAsset.NumberReference1,
            NumberReference2 = firstAsset.NumberReference2,
            NumberReference3 = firstAsset.NumberReference3,
            PreservedUri = firstAsset.PreservedUri,
            ThumbnailPolicy = firstAsset.ThumbnailPolicy,
            ImageOptimisationPolicy = firstAsset.ImageOptimisationPolicy,
            NotForDelivery = firstAsset.NotForDelivery,
            DeliveryChannels = firstAsset.DeliveryChannels.ToString().Split(","),
            ImageDeliveryChannels = GenerateImageDeliveryChannels(convertedRawAsset),
            Manifests = (firstAsset.Manifests as string[])?.ToList()
        };
    }

    private List<ImageDeliveryChannel> GenerateImageDeliveryChannels(List<dynamic> rawAsset)
    {
        var imageDeliveryChannels = new List<ImageDeliveryChannel>();
        foreach (dynamic rawDeliveryChannel in rawAsset)
        {
            if (rawDeliveryChannel.Channel != null) // avoids issues with left outer join returning assets without 'ImageDeliveryChannels'
            {
                imageDeliveryChannels.Add(new ImageDeliveryChannel()
                {
                    Channel = rawDeliveryChannel.Channel,
                    DeliveryChannelPolicyId = rawDeliveryChannel.DeliveryChannelPolicyId,
                });
            }
        }

        return imageDeliveryChannels;
    }

    private const string AdjunctSql =
        """
        SELECT a.Id, a.AssetId, a.Origin, a.IIIFLink, a.MediaType, a.Type
        FROM "Ajunct"
        WHERE a.Id = @Id AND a.AssetId = @AssetId
        """;
    
    private const string AssetSql =
        """
        SELECT "Images"."Id", "Customer", "Space", "Created", "Origin", "Tags", "Roles", 
        "PreservedUri", "Reference1", "Reference2", "Reference3", "MaxUnauthorised", "MaxWidth", "OpenFullMax",
        "NumberReference1", "NumberReference2", "NumberReference3", "Width", 
        "Height", "Error", "Batch", "Finished", "Ingesting", "ImageOptimisationPolicy", 
        "ThumbnailPolicy", "Family", "MediaType", "Duration", "NotForDelivery", "DeliveryChannels", "Manifests",
        IDC."Channel", IDC."DeliveryChannelPolicyId"
          FROM "Images"
          LEFT OUTER JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
          WHERE "Images"."Id"=@Id;
        """;

    private const string ImageLocationSql =
        "SELECT \"Id\", \"S3\", \"Nas\" FROM public.\"ImageLocation\" WHERE \"Id\"=@Id;";
}
