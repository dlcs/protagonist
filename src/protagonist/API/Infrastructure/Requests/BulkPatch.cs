using System.Collections.Generic;
using DLCS.HydraModel;
using Newtonsoft.Json;

namespace API.Infrastructure.Requests;

public class BulkPatch<T>
{
    [JsonProperty(Order = 30, PropertyName = "field")]
    public required string Field { get; set; }
    
    [JsonProperty(Order = 31, PropertyName = "operation")]
    public OperationType Operation { get; set; }
    
    [JsonProperty(Order = 32, PropertyName = "value")]
    public required List<string>? Value { get; set; }
    
    [JsonProperty(Order = 33, PropertyName = "member")]
    public T[]? Members { get; set; }
}
