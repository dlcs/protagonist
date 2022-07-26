using DLCS.Core.Guard;
using IIIF;

namespace Orchestrator.Models;

/// <summary>
/// Represents the results of a call to get a IIIF DescriptionResource (manifest, info.json etc)
/// </summary>
public class DescriptionResourceResponse
{
    public JsonLdBase? DescriptionResource { get; private init; }
    public bool HasResource { get; private init; }
    public bool RequiresAuth { get; private init; }
    public bool IsUnauthorised { get; private init; }
    public bool IsBadRequest { get; private init; }

    /// <summary>
    /// Get empty <see cref="DescriptionResourceResponse"/> result, containing no manifest.
    /// </summary>
    public static readonly DescriptionResourceResponse Empty = new();

    /// <summary>
    /// Get <see cref="DescriptionResourceResponse"/> for an open asset
    /// </summary>
    public static DescriptionResourceResponse Open(JsonLdBase resource) 
        => new()
        {
            DescriptionResource = resource.ThrowIfNull(nameof(resource))!,
            RequiresAuth = false,
            HasResource = true,
            IsUnauthorised = false
        };

    /// <summary>
    /// Get <see cref="DescriptionResourceResponse"/> for an restricted asset the user can access
    /// </summary>
    public static DescriptionResourceResponse Restricted(JsonLdBase? resource) 
        => new()
        {
            DescriptionResource = resource.ThrowIfNull(nameof(resource))!,
            RequiresAuth = true,
            HasResource = true,
            IsUnauthorised = false
        };
    
    /// <summary>
    /// Get <see cref="DescriptionResourceResponse"/> for an restricted asset the user cannot access
    /// </summary>
    public static DescriptionResourceResponse Unauthorised(JsonLdBase resource) 
        => new()
        {
            DescriptionResource = resource.ThrowIfNull(nameof(resource))!,
            RequiresAuth = true,
            HasResource = true,
            IsUnauthorised = true
        };

    /// <summary>
    /// Get <see cref="DescriptionResourceResponse"/> for a resource that couldn't be generated due to bad client
    /// request 
    /// </summary>
    public static DescriptionResourceResponse BadRequest()
        => new()
        {
            IsBadRequest = true
        };
}