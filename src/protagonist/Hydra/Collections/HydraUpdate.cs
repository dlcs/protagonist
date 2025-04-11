using Hydra.Model;
using Newtonsoft.Json;

namespace Hydra.Collections;

public class HydraUpdate<T> : HydraCollection<T>
{
    [JsonProperty(Order = 30, PropertyName = "field")]
    public required string Field { get; set; }
    
    [JsonProperty(Order = 31, PropertyName = "operation")]
    public OperationType Operation { get; set; }
    
    [JsonProperty(Order = 32, PropertyName = "value")]
    public required object Value { get; set; }
}
