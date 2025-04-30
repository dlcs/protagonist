using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace DLCS.Web.Response;

/// <summary>
/// A collection of options related to path generation.
/// </summary>
public class PathTemplateOptions
{
    /// <summary>
    /// The default path format for DLCS
    /// </summary>
    internal const string DefaultPathFormat = "/{prefix}/{version}/{customer}/{space}/{assetPath}";

    /// <summary>
    /// The default <see cref="PathTemplate"/> for DLCS
    /// </summary>
    internal static readonly PathTemplate DefaultPathTemplate = new() { Path = DefaultPathFormat };

    /// <summary>
    /// Default path template if no host-specific overrides found.
    /// </summary>
    public PathTemplate Default { get; init; } = DefaultPathTemplate;

    /// <summary>
    /// Collection of path template overrides, keyed by hostname.
    /// </summary>
    public Dictionary<string, PathTemplate> Overrides { get; init; } = new();

    /// <summary>
    /// "Overrides" dictionary as JSON blob, will be used to populate Overrides
    /// Added as convenience for setting per-env settings using string-based config settings like ParameterStore
    /// </summary>
    public string? OverridesAsJson { get; init; }

    /// <summary>
    /// Get <see cref="PathTemplate"/> for host. 
    /// </summary>
    /// <param name="host">Host to get template path for.</param>
    /// <returns>Returns path for host, or default if override not found.</returns>
    public PathTemplate GetPathTemplateForHost(string host)
        => Overrides.GetValueOrDefault(host, Default);
}

/// <summary>
/// Construct that stores host-specific path template and optional prefixReplacements for generating rewritten paths 
/// </summary>
/// <remarks>
/// TypeConverter allows simple string values from appSettings to be mapped to maintain backwards compat
/// </remarks>
[TypeConverter(typeof(PathTemplateConverter))]
public class PathTemplate
{
    /// <summary>
    /// Path containing replacement values
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// Optional prefix replacements, e.g. "iiif-img" => "images", "iiif-av" => "av"
    /// Used to generate value to use for "{prefix}" replacement
    /// </summary>
    public Dictionary<string, string> PrefixReplacements { get; set; } = new();
}

/// <summary>
/// Allows mapping appSettings directly to strongly typed <see cref="PathTemplate"/>.
/// This only supports mapping to/from string
/// </summary>
public class PathTemplateConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string stringVal
            ? new PathTemplate { Path = stringVal }
            : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value,
        Type destinationType) =>
        destinationType == typeof(string) && value is PathTemplate hostTemplate
            ? hostTemplate.Path
            : base.ConvertTo(context, culture, value, destinationType);
}
