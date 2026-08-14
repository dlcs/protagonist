using Newtonsoft.Json;

namespace Hydra.Collections;

/// <summary>
/// A collection of related Hydra resources, see https://www.hydra-cg.com/spec/latest/core/#collections 
/// </summary>
/// <remarks>
/// This doesn't have to be all the same type. But usually will be.
/// If you really have to mix, use object as T
/// </remarks>
public class HydraCollection<T> : JsonLdBaseWithHydraContext
{
    public override string Type => "Collection";

    [JsonProperty(Order = 10, PropertyName = "totalItems")]
    public int TotalItems { get; set; }

    [JsonProperty(Order = 11, PropertyName = "pageSize")]
    public int? PageSize { get; set; }

    [JsonProperty(Order = 20, PropertyName = "member")] // discrepancy between Hydra spec and example
    public T[]? Members { get; set; }

    [JsonProperty(Order = 90, PropertyName = "view")]
    [HydraLink(Description = "The view options for the collection of items", ReadOnly = true)]
    public PartialCollectionView? View { get; set; }
}
