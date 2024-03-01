using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

public class DeliveryChannel
{
    [JsonProperty(PropertyName = "@type")]
    public string? Context => "vocab:DeliveryChannel";
    
    [JsonProperty(Order = 11, PropertyName = "channel")]
    public string? Channel { get; set; }
    
    [JsonProperty(Order = 12, PropertyName = "policy")]
    public string? Policy { get; set; }
}