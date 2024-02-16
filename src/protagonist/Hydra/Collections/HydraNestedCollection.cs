using Newtonsoft.Json;

namespace Hydra.Collections;

public class HydraNestedCollection<T> : HydraCollection<T>
{
    public override string Type => "Collection";
    
    public HydraNestedCollection(string baseUrl, string id)
    {
        Id = $"{baseUrl}/{id}";
    }

    [JsonProperty(Order = 10, PropertyName = "title")]
    public string? Title { get; set; }
}