using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Orchestrator.Features.Manifests;

public static class AssetManifestX
{
    /// <summary>
    /// Includes data from additional tables required to build manifests
    /// </summary>
    public static IQueryable<Asset> IncludeRequiredDataForManifest(this IQueryable<Asset> assets)
    {
        return assets.Include(a =>
                a.AssetApplicationMetadata.Where(md => md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes))
            .Include(a => a.ImageDeliveryChannels);
    }
}