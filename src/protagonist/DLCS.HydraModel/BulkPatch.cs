using System.Collections.Generic;
using Hydra.Collections;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

public class BulkPatch<T> : HydraCollection<T>
{
    [JsonProperty(Order = 30, PropertyName = "field")]
    public required string Field { get; set; }
    
    [JsonProperty(Order = 31, PropertyName = "operation")]
    public OperationType Operation { get; set; }
    
    [JsonProperty(Order = 32, PropertyName = "value")]
    public required List<string>? Value { get; set; }
}
