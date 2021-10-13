using Newtonsoft.Json;

namespace Hydra.Model
{
    public class SupportedProperty
    {
        [JsonProperty(Order = 10, PropertyName = "property")]
        public Property Property { get; set; }

        [JsonProperty(Order = 11, PropertyName = "hydra:title")]
        public string Title { get; set; }

        [JsonProperty(Order = 12, PropertyName = "hydra:description")]
        public string Description { get; set; }

        [JsonProperty(Order = 12, PropertyName = "required")]
        public bool? Required { get; set; }

        [JsonProperty(Order = 12, PropertyName = "readonly")]
        public bool? ReadOnly { get; set; }

        [JsonProperty(Order = 12, PropertyName = "writeonly")]
        public bool? WriteOnly { get; set; }

        [JsonIgnore]
        public string UnstableNote { get; set; }
    }
}