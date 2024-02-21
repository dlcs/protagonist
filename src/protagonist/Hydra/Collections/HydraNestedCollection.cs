using System;
using Newtonsoft.Json;

namespace Hydra.Collections;

public class HydraNestedCollection<T> : HydraCollection<T>
{
    public override string Type => "Collection";
    
    public HydraNestedCollection(string baseUrl, string id)
    {
        Id = new Uri(new Uri(baseUrl), id).ToString();
    }

    [JsonProperty(Order = 10, PropertyName = "title")]
    public string? Title { get; set; }
}