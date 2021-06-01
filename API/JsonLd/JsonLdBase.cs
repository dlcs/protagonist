using Newtonsoft.Json;

namespace API.JsonLd
{
    /// <summary>
    /// Base class for all JsonLd objects
    /// </summary>
    public class JsonLdBase
    {
        [JsonProperty("@context", Order = 1)]
        public string Context { get; set; } = "http://www.w3.org/ns/hydra/context.jsonld";
        
        [JsonProperty("@id", Order = 2)]
        public string Id { get; set; }
        
        [JsonProperty("@type", Order = 3)]
        public string Type { get; set; }
    } 
}