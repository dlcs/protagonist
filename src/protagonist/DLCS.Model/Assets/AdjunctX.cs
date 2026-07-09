namespace DLCS.Model.Assets;

public static class AdjunctX
{
    /// <summary>
    /// Returns true if the adjunct is or will be hosted by DLCS (i.e. has an origin to ingest from).
    /// </summary>
    /// <remarks>External adjuncts have no origin and are not ingested.</remarks>
    public static bool IsHosted(this Adjunct adjunct) => !string.IsNullOrWhiteSpace(adjunct.Origin);

    /// <summary>
    /// Returns true if the adjunct's bytes count towards a customer's stored-adjunct <em>size</em>.
    /// </summary>
    /// <remarks>
    /// A hosted adjunct at an optimised origin keeps its bytes in the customer's origin, so Protagonist isn't storing
    /// them - its size must not count towards storage totals. External adjuncts aren't hosted at all.
    /// </remarks>
    public static bool CountsTowardStoredSize(this Adjunct adjunct) => adjunct.IsHosted() && !adjunct.Optimised;
}
