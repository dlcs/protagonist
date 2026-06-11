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

/// <summary>
/// Model containing parameters for filtering results (effectively a WHERE clause)
/// </summary>
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

/// <summary>
/// Model containing parameters for inclusion of additional fields (e.g. adjuncts)
/// </summary>
public class AssetInclude
{
    public string[]? Include { get; }

    public AssetInclude(string[]? includes)
    {
        var include = includes?
            .Where(i => IncludeFields.AllowedFields.Contains(i, StringComparer.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        if (include?.Length > 0) Include = include;
    }

    public bool IncludesField(string field) =>
        Include?.Contains(field, StringComparer.OrdinalIgnoreCase) ?? false;
}

/// <summary>
/// Valid/known/accepted include parameters
/// </summary>
public static class IncludeFields
{
    public const string Adjuncts = "adjuncts";

    public static readonly string[] AllowedFields = [Adjuncts];
}

/// <summary>
/// QueryParameters used in Asset queries
/// </summary>
public static class QueryParameters
{
    /// <summary>
    /// The "q" parameter is used to specify serialised AssetQueryModel
    /// </summary>
    public const string Query = "q";
    
    /// <summary>
    /// The "include" parameter is used to specify fields to include in the response
    /// </summary>
    public const string Include = "include";
}
