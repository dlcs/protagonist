using System;
using System.Linq;

namespace DLCS.Model.Assets;

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
    public string[]? Tags { get; set; }
    public string[]? Include { get; set; }

    public bool IncludesField(string field) =>
        Include?.Contains(field, StringComparer.OrdinalIgnoreCase) ?? false;
}

public static class IncludeFields
{
    public const string Adjuncts = "adjuncts";
}
