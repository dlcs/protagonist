namespace Orchestrator.Infrastructure.IIIF.Manifests;

/// <summary>
/// Enum representing the type of Manifest that can be created
/// </summary>
public enum ManifestType
{
    /// <summary>
    /// Manifest that contains details of single item
    /// </summary>
    SingleItem = 0,
    
    /// <summary>
    /// Manifest that contains assets that are result of a NamedQuery
    /// </summary>
    NamedQuery = 1,
}
