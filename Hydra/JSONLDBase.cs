using Newtonsoft.Json;

namespace Hydra
{
    public abstract class JSONLDBase
    {
        [JsonProperty(Order = 1, PropertyName = "@context")]
        public virtual string Context { get; set; }

        [JsonProperty(Order = 2, PropertyName = "@id")]
        public string Id { get; set; }

        [JsonProperty(Order = 3, PropertyName = "@type")]
        public virtual string Type { get; set; }
    }
}