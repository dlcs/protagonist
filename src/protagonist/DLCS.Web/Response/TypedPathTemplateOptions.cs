using System.Collections.Generic;

// ReSharper disable CollectionNeverUpdated.Global

namespace DLCS.Web.Response;

/// <summary>
/// A collection of options related to path generation. This differs from <see cref="PathTemplateOptions"/> as this
/// models objects with differing templates depending on 'type' parameter.
/// </summary>
public class TypedPathTemplateOptions
{
    /// <summary>
    /// Default path names for the different types of path
    /// </summary>
    public Dictionary<string, string> Defaults { get; set; } = new();
    
    /// <summary>
    /// Collection of path template overrides, these are keyed by "hostname" and sub-dictionary keyed by type
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Overrides { get; set; } = new();

    /// <summary>
    /// Get template path for host. 
    /// </summary>
    /// <param name="host">Host to get template path for.</param>
    /// <param name="type">Type of item to get template path for.</param>
    /// <returns>Returns path for host, or default if override not found.</returns>
    public string GetPathTemplateForHostAndType(string host, string type)
    {
        if (Overrides.TryGetValue(host, out var hostLevel))
        {
            return hostLevel.TryGetValue(type, out var hostTemplate) ? hostTemplate : GetPathTemplateForType(type);
        }

        return GetPathTemplateForType(type);
    }

    private string GetPathTemplateForType(string type)
    {
        if (Defaults.TryGetValue(type, out var template))
        {
            return template;
        }

        throw new KeyNotFoundException($"Could not find default path template for type: {type}");
    }
}