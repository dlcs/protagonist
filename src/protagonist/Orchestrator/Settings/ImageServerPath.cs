namespace Orchestrator.Settings;

/// <summary>
/// Represents the path to an asset on the downstream image-server, split into the prefix (e.g. "/iiif/3/") and the
/// IIIF ImageApi {identifier} that the image-server uses to locate the asset.
/// </summary>
/// <param name="Prefix">The IIIF ImageApi {prefix}</param>
/// <param name="Identifier">
/// The IIIF ImageApi {identifier}, as it appears in the request sent to the image-server (ie url-encoded)
/// </param>
public record ImageServerPath(string Prefix, string Identifier)
{
    /// <summary>
    /// Full path to asset on image-server, ie {prefix}{identifier}
    /// </summary>
    public string FullPath { get; } = $"{Prefix}{Identifier}";

    /// <summary>The IIIF ImageApi {prefix}</summary>
    public string Prefix { get; init; } = Prefix;

    public override string ToString() => FullPath;
}
