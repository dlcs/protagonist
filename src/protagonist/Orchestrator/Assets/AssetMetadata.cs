using System.Collections.Generic;
using DLCS.Model.Assets;

namespace Orchestrator.Assets;

/// <summary>
/// Descriptive metadata of an <see cref="Asset"/>, for use during orchestration (e.g. evaluating appointment roles)
/// </summary>
public record AssetMetadata
{
    public IReadOnlyList<string>? Manifests { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? Reference1 { get; init; }
    public string? Reference2 { get; init; }
    public string? Reference3 { get; init; }
    public int? NumberReference1 { get; init; }
    public int? NumberReference2 { get; init; }
    public int? NumberReference3 { get; init; }

    public static AssetMetadata From(Asset asset)
        => new()
        {
            Manifests = asset.Manifests,
            Tags = asset.Tags,
            Reference1 = asset.Reference1,
            Reference2 = asset.Reference2,
            Reference3 = asset.Reference3,
            NumberReference1 = asset.NumberReference1,
            NumberReference2 = asset.NumberReference2,
            NumberReference3 = asset.NumberReference3,
        };
}
