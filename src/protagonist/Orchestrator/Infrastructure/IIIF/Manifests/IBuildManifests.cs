using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using IIIF;
using PresentationApiVersion = IIIF.Presentation.Version;

namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Interface for construction of a manifest
/// </summary>
/// <typeparam name="T">Type of manifest to build</typeparam>
public interface IBuildManifests<T>
    where T : JsonLdBase
{
    Task<T> BuildManifest(string manifestId, string label, List<Asset> assets, CustomerPathElement customerPathElement,
        ManifestType manifestType, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for manifest building, ensures that <see cref="IManifestBuilderUtils"/> is configured with correct
/// version 
/// </summary>
public abstract class ManifestBuilderBase<T>(IManifestBuilderUtils builderUtils) : IBuildManifests<T>
    where T : JsonLdBase
{
    protected readonly IManifestBuilderUtils BuilderUtils = builderUtils;

    public Task<T> BuildManifest(string manifestId, string label, List<Asset> assets, CustomerPathElement customerPathElement,
        ManifestType manifestType, CancellationToken cancellationToken)
    {
        BuilderUtils.SetPresentationVersion(PresentationApiVersion);
        return BuildManifestImpl(manifestId, label, assets, customerPathElement, manifestType, cancellationToken);
    }

    protected abstract PresentationApiVersion PresentationApiVersion { get; }

    protected abstract Task<T> BuildManifestImpl(string manifestId, string label, List<Asset> assets,
        CustomerPathElement customerPathElement, ManifestType manifestType, CancellationToken cancellationToken);
}
