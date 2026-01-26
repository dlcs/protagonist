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
    /// This currently only returns adjuncts with an ExternalId, which is currently ALL adjuncts. Future changes will
    /// make ExternalId nullable, so this clause will prevent those appearing in Manifests until we want them. It can
    /// be removed when DLCS hosted assets are available. 
    /// </remarks>
    public static IQueryable<Asset> IncludeRelationsForProjections(this IQueryable<Asset> assets) =>
        assets.Include(a =>
                a.AssetApplicationMetadata.Where(md =>
                    md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes ||
                    md.MetadataType == AssetApplicationMetadataTypes.AVTranscodes))
            .Include(a => a.ImageDeliveryChannels)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            .Include(a => a.Adjuncts!.Where(adj => adj.ExternalId != null).OrderBy(ad => ad.Id));
}
