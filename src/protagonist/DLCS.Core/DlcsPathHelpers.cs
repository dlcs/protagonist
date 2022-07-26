namespace DLCS.Core;

/// <summary>
/// A collection of methods to make dealing with DLCS paths, and path replacements, easier
/// </summary>
public static class DlcsPathHelpers
{
    /// <summary>
    /// Replace known slugs in DLCS path template.
    /// </summary>
    /// <param name="template">DLCS path template, including slugs to replace</param>
    /// <param name="prefix">Value to replace {prefix} with</param>
    /// <param name="customer">Value to replace {customer} with</param>
    /// <param name="space">Value to replace {space} with</param>
    /// <param name="assetPath">Value to replace {assetPath} with</param>
    /// <returns>Template with string replacements made</returns>
    public static string GeneratePathFromTemplate(
        string template, 
        string? prefix = null, 
        string? customer = null, 
        string? space = null,
        string? assetPath = null) =>
        template
            .Replace("{prefix}", prefix ?? string.Empty)
            .Replace("{customer}", customer ?? string.Empty)
            .Replace("{space}", space ?? string.Empty)
            .Replace("{assetPath}", assetPath ?? string.Empty);
}