using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using Orchestrator.Infrastructure.IIIF.Manifests;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Class for creating IIIF Manifests from provided assets 
/// </summary>
public class IIIFManifestBuilder
{
    private readonly IBuildManifests<IIIF3.Manifest> manifestV3Builder;
    private readonly IBuildManifests<IIIF2.Manifest> manifestV2Builder;

    public IIIFManifestBuilder(IBuildManifests<IIIF3.Manifest> manifestV3Builder, 
        IBuildManifests<IIIF2.Manifest> manifestV2Builder)
    {
        this.manifestV3Builder = manifestV3Builder;
        this.manifestV2Builder = manifestV2Builder;
    }

    public Task<IIIF3.Manifest> GenerateV3Manifest(List<Asset> assets, CustomerPathElement customerPathElement,
        string manifestId, string label, ManifestType manifestType, CancellationToken cancellationToken)
        => manifestV3Builder.BuildManifest(manifestId, label, assets, customerPathElement, manifestType,
            cancellationToken);

    public Task<IIIF2.Manifest> GenerateV2Manifest(List<Asset> assets, CustomerPathElement customerPathElement,
        string manifestId, string label, ManifestType manifestType, CancellationToken cancellationToken)
        => manifestV2Builder.BuildManifest(manifestId, label, assets, customerPathElement, manifestType,
            cancellationToken);
}
