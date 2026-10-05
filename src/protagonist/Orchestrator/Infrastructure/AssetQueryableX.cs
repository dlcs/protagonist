using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Orchestrator.Infrastructure;

public static class AssetQueryableX
{
    /// <summary>
    /// Includes data from <see cref="AssetApplicationMetadata"/> and related <see cref="ImageDeliveryChannel"/> and
    /// <see cref="Adjunct"/> that are relevant to Orchestrator processing manifests and named query projections
    /// </summary>
    /// <remarks>
    /// Returns adjuncts that are either external (have an ExternalId) or hosted by DLCS (have an Origin).
    /// </remarks>
    public static IQueryable<Asset> IncludeRelationsForProjections(this IQueryable<Asset> assets) =>
        assets.Include(a =>
                a.AssetApplicationMetadata.Where(md =>
                    md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes ||
                    md.MetadataType == AssetApplicationMetadataTypes.AVTranscodes))
            .Include(a => a.ImageDeliveryChannels)
            .Include(a => a.Adjuncts!.Where(adj => adj.ExternalId != null || adj.Origin != null).OrderBy(ad => ad.Id));
}
