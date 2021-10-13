using Newtonsoft.Json;

namespace Hydra.Model
{
    public class Property : JSONLDBaseWithHydraContext
    {
        [JsonProperty(Order = 11, PropertyName = "label")]
        public string Label { get; set; }

        [JsonProperty(Order = 12, PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(Order = 13, PropertyName = "domain")]
        public string Domain { get; set; }

        [JsonProperty(Order = 14, PropertyName = "range")]
        public string Range { get; set; }

    }

    public class RdfProperty : Property
    {
        public override string Type
        {
            get { return "rdf:Property"; }
        }
    }

    public class HydraLinkProperty : Property
    {
        public override string Type
        {
            get { return "hydra:Link"; }
        }

        [JsonProperty(Order = 99, PropertyName = "supportedOperation")]
        public Operation[] SupportedOperations { get; set; }
    }
}