using System.Text.RegularExpressions;
using DLCS.Core.Formats;
using DLCS.Core.Types;

namespace DLCS.Core;

/// <summary>
/// A collection of methods to make dealing with DLCS paths, and path replacements, easier
/// </summary>
public static class DlcsPathHelpers
{
    private static readonly Regex DoubleSlashRegex = new(@"(?<!http[s]?:)\/\/+", RegexOptions.Compiled);

    /// <summary>
    /// Replace known slugs in DLCS path template.
    /// </summary>
    /// <param name="template">DLCS path template, including slugs to replace</param>
    /// <param name="prefix">Value to replace {prefix} with</param>
    /// <param name="version">Value to replace {version} with</param>
    /// <param name="customer">Value to replace {customer} with</param>
    /// <param name="space">Value to replace {space} with</param>
    /// <param name="assetPath">Value to replace {assetPath} with, optionally formatted</param>
    /// <returns>Template with string replacements made</returns>
    public static string GeneratePathFromTemplate(
        string template,
        string? prefix = null,
        string? version = null,
        string? customer = null,
        string? space = null,
        string? assetPath = null)
        => DoubleSlashRegex.Replace(
            template
                .Replace(Replacements.Prefix, prefix ?? string.Empty)
                .Replace(Replacements.Version, version ?? string.Empty)
                .Replace(Replacements.Customer, customer ?? string.Empty)
                .Replace(Replacements.Space, space ?? string.Empty)
                .ReplaceAssetPath(assetPath ?? string.Empty), "/");

    /// <summary>
    /// Replace known slugs in DLCS auth path template.
    /// </summary>
    /// <param name="template">DLCS auth path template, including slugs to replace</param>
    /// <param name="customer">Value to replace {customer} with</param>
    /// <param name="behaviour">Value to replace {behaviour} with</param>
    /// <returns>Template with string replacements made</returns>
    public static string GenerateAuthPathFromTemplate(
        string template, 
        string? customer = null, 
        string? behaviour = null) =>
        template
            .Replace(Replacements.Customer, customer ?? string.Empty)
            .Replace(Replacements.Behaviour, behaviour ?? string.Empty);
    
    /// <summary>
    /// Replace known slugs in DLCS auth 2 path template.
    /// </summary>
    /// <param name="template">DLCS auth path template, including slugs to replace</param>
    /// <param name="assetId">Value to replace {assetId} with</param>
    /// <param name="customer">Value to replace {customer} with</param>
    /// <param name="accessService">Value to replace {accessService} with</param>
    /// <returns>Template with string replacements made</returns>
    public static string GenerateAuth2PathFromTemplate(
        string template,
        AssetId? assetId = null,
        string? customer = null,
        string? accessService = null)
    {
        if (assetId != null)
        {
            template = template
                .Replace(Replacements.AssetId, assetId.ToString())
                .Replace(Replacements.Asset, assetId.Asset)
                .Replace(Replacements.Space, assetId.Space.ToString());
        }
        
        return template
            .Replace(Replacements.Customer, customer ?? string.Empty)
            .Replace(Replacements.AccessService, accessService ?? string.Empty);
    }
    
    /// <summary>
    /// Class containing path values that are replaceable
    /// </summary>
    public static class Replacements
    {
        public const string Prefix = "{prefix}";
        public const string Version = "{version}";
        public const string Customer = "{customer}";
        public const string Space = "{space}";
        public const string Behaviour = "{behaviour}";
        public const string AssetId = "{assetId}";
        public const string Asset = "{asset}";
        public const string AccessService = "{accessService}";
    }
}
