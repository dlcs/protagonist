using System;
using System.Text.RegularExpressions;

namespace DLCS.Core.Formats;

/// <summary>
/// Helper function for formatting {assetPath} template value, handling replacements 
/// </summary>
internal static class AssetPathFormatter
{
    // match {assetPath} or {assetPath:FMT}
    private static readonly Regex AssetPath = new("({assetPath:?.*})", RegexOptions.Compiled);
    
    public static string ReplaceAssetPath(this string template, string assetPath)
    {
        var match = AssetPath.Match(template);
        if (!match.Success) return template;

        for (var x = 0; x < match.Captures.Count; x++)
        {
            var capture = match.Captures[x].Value;
            var forFormat = capture.Replace("assetPath", "0");
            template = template.Replace(capture, string.Format(AssetPathFormat.Instance, forFormat, assetPath));
        }

        return template;
    }
}
 
internal class AssetPathFormat : IFormatProvider, ICustomFormatter
{
    public static AssetPathFormat Instance { get; } = new();

    // Replace "___" with "/"
    private const string UnderscoreToSlash = "3US"; 
    
    public object? GetFormat(Type? formatType) 
        => formatType == typeof(ICustomFormatter) ? this : null;

    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
    {
        if (string.IsNullOrEmpty(format) || arg == null) return arg?.ToString() ?? string.Empty;
		
        var result = arg.ToString();
        if (string.IsNullOrEmpty(result)) return string.Empty;
        
        if (format == UnderscoreToSlash)
        {
            return result.Replace("___", "/");
        }

        throw new ArgumentException($"'{format}' is not a known assetPath format", nameof(format));
    }
}
