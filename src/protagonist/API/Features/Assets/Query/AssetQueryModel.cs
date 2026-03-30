namespace API.Features.Assets.Query;

/// <summary>
/// Strongly typed model for Asset queries (Asset Query Syntax)
/// </summary>
/// <param name="filter">Parameters for filtering results (effectively a WHERE clause)</param>
/// <param name="include">Parameters for inclusion of additional fields (e.g. adjuncts)</param>
/// <remarks>See https://dlcs.github.io/public-docs/api-doc/asset-queries/</remarks>
public class AssetQueryModel(AssetFilter? filter, AssetInclude? include)
{
    public AssetFilter? Filter { get; } = filter;
    public AssetInclude? Include { get; } = include;

    public bool IncludesField(string field) => Include?.IncludesField(field) ?? false;
}

public class AssetFilter
{
    public int? Space { get; set; }
    public string? Reference1 { get; set; }
    public string? Reference2 { get; set; }
    public string? Reference3 { get; set; }
    public int? NumberReference1 { get; set; }
    public int? NumberReference2 { get; set; }
    public int? NumberReference3 { get; set; }
    public string[]? Manifests { get; set; }
}

public class AssetInclude
{
    public string[]? Include { get; set; }

    public bool IncludesField(string field) =>
        Include?.Contains(field, StringComparer.OrdinalIgnoreCase) ?? false;
}

public static class IncludeFields
{
    public const string Adjuncts = "adjuncts";
}
