namespace DLCS.Model.Assets;

public static class AdjunctX
{
    /// <summary>
    /// Returns true if the adjunct is or will be hosted by DLCS (i.e. has an origin to ingest from).
    /// </summary>
    /// <remarks>External adjuncts have no origin and are not ingested.</remarks>
    public static bool IsHosted(this Adjunct adjunct) => !string.IsNullOrWhiteSpace(adjunct.Origin);
}
