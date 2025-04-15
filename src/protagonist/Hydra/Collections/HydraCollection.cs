using Newtonsoft.Json;

namespace Hydra.Collections;

/// <summary>
/// This doesn't have to be all the same type. But usually will be.
/// If you really have to mix, use object.
/// </summary>
/// <typeparam name="T"></typeparam>
public class HydraCollection<T> : JsonLdBaseWithHydraContext, IMember<T>
{
    public override string Type => "Collection";

    [JsonProperty(Order = 10, PropertyName = "totalItems")]
    public int TotalItems { get; set; }

    [JsonProperty(Order = 11, PropertyName = "pageSize")]
    public int? PageSize { get; set; }

    [JsonProperty(Order = 20, PropertyName = "member")] // discrepancy between Hydra spec and example
    public T[]? Members { get; set; }

    [JsonProperty(Order = 90, PropertyName = "view")]
    public PartialCollectionView? View { get; set; }
}
