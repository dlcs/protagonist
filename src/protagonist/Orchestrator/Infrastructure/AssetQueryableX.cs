using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Orchestrator.Infrastructure;

public static class AssetQueryableX
{
    /// <summary>
    /// Includes data from <see cref="AssetApplicationMetadata"/> and related <see cref="ImageDeliveryChannel"/> that is
    /// relevant to Orchestrator processing for manifests and named query projections
    /// </summary>
    public static IQueryable<Asset> IncludeRelevantMetadata(this IQueryable<Asset> assets) =>
        assets.Include(a =>
                a.AssetApplicationMetadata.Where(md =>
                    md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes ||
                    md.MetadataType == AssetApplicationMetadataTypes.AVTranscodes))
            .Include(a => a.ImageDeliveryChannels);
}
