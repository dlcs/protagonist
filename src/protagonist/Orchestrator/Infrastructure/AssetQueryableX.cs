using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Orchestrator.Infrastructure;

public static class AssetQueryableX
{
    /// <summary>
    /// Includes data from additional tables required to build manifests
    /// </summary>
    public static IQueryable<Asset> IncludeDataForThumbs(this IQueryable<Asset> assets)
    {
        return assets.Include(a =>
                Enumerable.Where<AssetApplicationMetadata>(a.AssetApplicationMetadata, md => md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes))
            .Include(a => a.ImageDeliveryChannels);
    }
}