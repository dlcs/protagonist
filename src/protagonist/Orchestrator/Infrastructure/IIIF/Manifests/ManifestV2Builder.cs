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
using DLCS.Web.Response;
using IIIF.Presentation;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Annotation;
using IIIF.Presentation.V2.Strings;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Implementation of <see cref="IBuildManifests{T}"/> responsible for generating IIIF v2 manifest
/// </summary>
public class ManifestV2Builder : IIIFManifestBuilderBase, IBuildManifests<Manifest>
{
    public ManifestV2Builder(IAssetPathGenerator assetPathGenerator,
        IOptions<OrchestratorSettings> orchestratorSettings, IThumbSizeProvider thumbSizeProvider) : base(
        assetPathGenerator, orchestratorSettings, thumbSizeProvider)
    {
    }

    public async Task<Manifest> BuildManifest(string manifestId, string label, List<Asset> assets,
        CustomerPathElement customerPathElement, ManifestType manifestType, CancellationToken cancellationToken)
    {
        var manifest = new Manifest
        {
            Id = manifestId,
            Label = new MetaDataValue(label),
            Metadata = GetManifestMetadata().ToV2Metadata(),
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
            // TODO - this breaks tests but makes sense - cleans up ImageResource construction below
            //if (!asset.HasDeliveryChannel(AssetDeliveryChannels.Image)) continue;

            var canvasId = GetCanvasId(asset, customerPathElement, ++counter);
            var thumbnailSizes = await RetrieveThumbnails(asset, cancellationToken);

            var canvas = new Canvas
            {
                Id = canvasId,
                Label = new MetaDataValue($"Canvas {counter}"),
                Width = asset.Width,
                Height = asset.Height,
                Metadata = GetCanvasMetadata(asset).ToV2Metadata(),
                Images = new ImageAnnotation
                {
                    Id = string.Concat(canvasId, "/imageanno/0"),
                    On = canvasId,
                    Resource = asset.HasDeliveryChannel(AssetDeliveryChannels.Image)
                        ? new ImageResource
                        {
                            Id = GetFullQualifiedImagePath(asset, customerPathElement,
                                thumbnailSizes.MaxDerivativeSize, false),
                            Width = thumbnailSizes.MaxDerivativeSize.Width,
                            Height = thumbnailSizes.MaxDerivativeSize.Height,
                            Service = GetImageServices(asset, customerPathElement, true, null)
                        }
                        : null,
                }.AsList()
            };

            if (ShouldAddThumbs(asset, thumbnailSizes))
            {
                canvas.Thumbnail = new Thumbnail
                {
                    Id = GetFullQualifiedThumbPath(asset, customerPathElement, thumbnailSizes.OpenThumbnails),
                    Service = GetImageServiceForThumbnail(asset, customerPathElement, true,
                        thumbnailSizes.OpenThumbnails)
                }.AsList();
            }

            canvases.Add(canvas);
        }

        return canvases;
    }
}
