using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using IIIF;

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
