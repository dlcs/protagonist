using System.Collections.Generic;
using DLCS.Core.Strings;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure.Auth.V2;

/// <summary>
/// Body of POST requests to IIIF Auth2 probe_internal and verifyaccess endpoints. Serialised camelCase, nulls omitted
/// </summary>
/// <param name="Roles">Roles that the item has, any of which grants access</param>
/// <param name="Asset">Asset metadata, used to evaluate appointment roles</param>
public record AccessCheckRequest(IReadOnlyList<string> Roles, AccessCheckAsset? Asset);

/// <summary>
/// Asset metadata, as named by IIIF Auth2 service. This is matched against the asset patterns of appointments held by
/// the user. Customer and space are not included as they are part of the deliverable id
/// </summary>
public record AccessCheckAsset
{
    /// <summary>DLCS asset Manifests</summary>
    public IReadOnlyList<string>? Scopes { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>DLCS asset Reference1</summary>
    public string? String1 { get; init; }

    /// <summary>DLCS asset Reference2</summary>
    public string? String2 { get; init; }

    /// <summary>DLCS asset Reference3</summary>
    public string? String3 { get; init; }

    /// <summary>DLCS asset NumberReference1</summary>
    public int? Number1 { get; init; }

    /// <summary>DLCS asset NumberReference2</summary>
    public int? Number2 { get; init; }

    /// <summary>DLCS asset NumberReference3</summary>
    public int? Number3 { get; init; }

    public static AccessCheckAsset From(AssetMetadata metadata)
        => new()
        {
            Scopes = metadata.Manifests,
            Tags = metadata.Tags,
            String1 = NullIfNoText(metadata.Reference1),
            String2 = NullIfNoText(metadata.Reference2),
            String3 = NullIfNoText(metadata.Reference3),
            Number1 = metadata.NumberReference1,
            Number2 = metadata.NumberReference2,
            Number3 = metadata.NumberReference3,
        };

    // DB columns are non-nullable so unset references are empty. Sent as null so they're treated as no value. Numbers
    // can't be treated the same as 0 is a valid value
    private static string? NullIfNoText(string? value) => value.HasText() ? value : null;
}
