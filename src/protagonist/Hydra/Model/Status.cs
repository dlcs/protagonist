using Newtonsoft.Json;

namespace Hydra.Model;

public class Status : JsonLdBaseWithHydraContext
{
    public override string Type => "Status";

    [JsonProperty(Order = 10, PropertyName = "statusCode")]
    public int StatusCode { get; set; }

    [JsonProperty(Order = 11, PropertyName = "title")]
    public string? Title { get; set; }

    [JsonProperty(Order = 12, PropertyName = "description")]
    public string? Description { get; set; }
}