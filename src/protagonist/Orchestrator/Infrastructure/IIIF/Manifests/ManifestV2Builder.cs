using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.IIIF;
using DLCS.Model.PathElements;
using IIIF.Presentation;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using PresentationApiVersion = IIIF.Presentation.Version;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Implementation of <see cref="IBuildManifests{T}"/> responsible for generating IIIF v2 manifest
/// </summary>
public class ManifestV2Builder : ManifestBuilderBase<Manifest>
{
    /// <summary>
    /// Implementation of <see cref="IBuildManifests{T}"/> responsible for generating IIIF v2 manifest
    /// </summary>
    public ManifestV2Builder(IManifestBuilderUtils builderUtils) : base(builderUtils)
    {
    }

    protected override PresentationApiVersion PresentationApiVersion => PresentationApiVersion.V2;

    protected override async Task<Manifest> BuildManifestImpl(string manifestId, string label, List<Asset> assets, CustomerPathElement customerPathElement,
        ManifestType manifestType, CancellationToken cancellationToken)
    {
        var manifest = new Manifest
        {
            Id = manifestId,
            Label = new MetaDataValue(label),
            Metadata = ManifestBuilderUtils.GetManifestMetadata().ToV2Metadata(),
        };

        manifest.EnsurePresentation2Context();

        var canvases = await CreateCanvases(assets, customerPathElement, cancellationToken);
        var sequence = new Sequence
        {
            Id = GetSequenceId(manifestId, manifestType),
            Label = new MetaDataValue("Sequence 0"),
        };
        sequence.Canvases = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;

        manifest.Sequences = sequence.AsList();
        return manifest;
    }

    private static string GetSequenceId(string manifestId, ManifestType manifestType)
    {
        if (manifestType == ManifestType.NamedQuery)
        {
            // for named-query manifests the sequence id is a generic https://{host}/iiif-query/ root
            var root = manifestId[..manifestId.IndexOf("iiif-resource", StringComparison.Ordinal)];
            return root.ToConcatenated('/', "iiif-query", "sequence", "0");
        }

        // single-item manifests use the image url as root
        return manifestId.ToConcatenated('/', "/sequence/0");
    }

    private async Task<List<Canvas>> CreateCanvases(List<Asset> results, CustomerPathElement customerPathElement,
        CancellationToken cancellationToken)
    {
        int counter = 0;
        var canvases = new List<Canvas>(results.Count);
        foreach (var asset in results)
        {
            var canvasId = BuilderUtils.GetCanvasId(asset, customerPathElement, ++counter);
            var thumbnailSizes = await BuilderUtils.RetrieveThumbnails(asset, cancellationToken);

            var canvas = new Canvas
            {
                Id = canvasId,
                Label = new MetaDataValue($"Canvas {counter}"),
                Width = asset.Width,
                Height = asset.Height,
                Metadata = ManifestBuilderUtils.GetCanvasMetadata(asset).ToV2Metadata(),
                Images = new ImageAnnotation
                {
                    Id = string.Concat(canvasId, "/imageanno/0"),
                    On = canvasId,
                    Resource = asset.HasDeliveryChannel(AssetDeliveryChannels.Image)
                        ? new ImageResource
                        {
                            Id = BuilderUtils.GetFullQualifiedImagePath(asset, customerPathElement,
                                thumbnailSizes.MaxDerivativeSize, false),
                            Width = thumbnailSizes.MaxDerivativeSize.Width,
                            Height = thumbnailSizes.MaxDerivativeSize.Height,
                            Service = BuilderUtils.GetImageServices(asset, customerPathElement, null)
                        }
                        : null,
                }.AsList()
            };

            if (BuilderUtils.ShouldAddThumbs(asset, thumbnailSizes))
            {
                var targetThumbnail =
                    BuilderUtils.GetFullQualifiedThumb(asset, customerPathElement, thumbnailSizes.OpenThumbnails);
                canvas.Thumbnail = new Thumbnail
                {
                    Id = targetThumbnail.Path,
                    Service = BuilderUtils.GetImageServiceForThumbnail(asset, customerPathElement,
                        thumbnailSizes.OpenThumbnails)
                }.AsList();
            }

            canvases.Add(canvas);
        }

        return canvases;
    }
}
