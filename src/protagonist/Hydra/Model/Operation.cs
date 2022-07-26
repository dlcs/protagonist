using Newtonsoft.Json;

namespace Hydra.Model
{
    public class Operation : JsonLdBaseWithHydraContext
    {
        public override string Type
        {
            get { return "hydra:Operation"; }
        }

        [JsonProperty(Order = 11, PropertyName = "method")]
        public string Method { get; set; }

        [JsonProperty(Order = 12, PropertyName = "label")]
        public string Label { get; set; }

        [JsonProperty(Order = 13, PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(Order = 14, PropertyName = "expects")]
        public string Expects { get; set; }

        [JsonProperty(Order = 15, PropertyName = "returns")]
        public string Returns { get; set; }

        [JsonProperty(Order = 20, PropertyName = "statusCode")]
        public Status[] StatusCodes { get; set; }
    }
}