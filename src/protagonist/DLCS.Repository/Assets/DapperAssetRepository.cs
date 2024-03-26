using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IAssetRepository"/> using Dapper for data access.
/// </summary>
public class DapperAssetRepository : IAssetRepository, IDapperConfigRepository
{
    public IConfiguration Configuration { get; }
    private readonly AssetCachingHelper assetCachingHelper;
    
    public DapperAssetRepository(
        IConfiguration configuration, 
        AssetCachingHelper assetCachingHelper)
    {
        Configuration = configuration;
    }
    
    public async Task<ImageLocation?> GetImageLocation(AssetId assetId)
        => await this.QuerySingleOrDefaultAsync<ImageLocation>(ImageLocationSql, new {Id = assetId.ToString()});
    
    public async Task<Asset?> GetAsset(AssetId assetId)
    {
        var asset = await assetCachingHelper.GetCachedAsset(assetId, GetAssetInternal);
        return asset;
    }
    
    private async Task<Asset?> GetAssetInternal(AssetId assetId)
    {
        var id = assetId.ToString();
        IEnumerable<dynamic> rawAsset = await this.QueryAsync(AssetSql, new { Id = id });
        var convertedRawAsset = rawAsset.ToList();
        if (!convertedRawAsset.Any())
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
            MediaType = firstAsset.MediaType,
            NumberReference1 = firstAsset.NumberReference1,
            NumberReference2 = firstAsset.NumberReference2,
            NumberReference3 = firstAsset.NumberReference3,
            PreservedUri = firstAsset.PreservedUri,
            ThumbnailPolicy = firstAsset.ThumbnailPolicy,
            ImageOptimisationPolicy = firstAsset.ImageOptimisationPolicy,
            NotForDelivery = firstAsset.NotForDelivery,
            DeliveryChannels = firstAsset.DeliveryChannels.ToString().Split(","),
            ImageDeliveryChannels = GenerateImageDeliveryChannels(convertedRawAsset)
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
                    DeliveryChannelPolicy = new DeliveryChannelPolicy()
                    {
                        PolicyData = rawDeliveryChannel.PolicyData,
                    }
                });
            }
        }

        return imageDeliveryChannels;
    }

    private const string AssetSql = @"
SELECT ""Images"".""Id"", ""Images"".""Customer"", ""Space"", ""Images"".""Created"", ""Origin"", ""Tags"", ""Roles"", 
""PreservedUri"", ""Reference1"", ""Reference2"", ""Reference3"", ""MaxUnauthorised"", 
""NumberReference1"", ""NumberReference2"", ""NumberReference3"", ""Width"", 
""Height"", ""Error"", ""Batch"", ""Finished"", ""Ingesting"", ""ImageOptimisationPolicy"", 
""ThumbnailPolicy"", ""Family"", ""MediaType"", ""Duration"", ""NotForDelivery"", ""DeliveryChannels"",  
IDC.""Channel"", IDC.""DeliveryChannelPolicyId"", ""PolicyData""
  FROM ""Images""
  LEFT OUTER JOIN ""ImageDeliveryChannels"" IDC on ""Images"".""Id"" = IDC.""ImageId""
  JOIN ""DeliveryChannelPolicies"" DCP ON IDC.""DeliveryChannelPolicyId"" = DCP.""Id""
  WHERE ""Images"".""Id""=@Id;";

    private const string ImageLocationSql =
        "SELECT \"Id\", \"S3\", \"Nas\" FROM public.\"ImageLocation\" WHERE \"Id\"=@Id;";
}