namespace DLCS.Model.Assets;

public static class AdjunctX
{
    /// <summary>
    /// Semantic helper for code readability. If `origin` has been provided in adjunct, it needs to be ingested into hosted adjunct
    /// </summary>
    /// <param name="adjunct">Adjunct for which to determine whether it should be ingested</param>
    /// <returns></returns>
    public static bool IsToBeIngested(this Adjunct adjunct) => adjunct.Origin is { Length: > 0 };
}
