using Newtonsoft.Json;

namespace API.Client.OldJsonLd
{
    public class PartialCollectionView : OldJsonLdBase
    {
        public override string Type
        {
            get { return "PartialCollectionView"; }
        }

        [JsonProperty(Order = 11, PropertyName = "first")]
        public string First { get; set; }

        [JsonProperty(Order = 12, PropertyName = "previous")]
        public string Previous { get; set; }

        [JsonProperty(Order = 13, PropertyName = "next")]
        public string Next { get; set; }

        [JsonProperty(Order = 14, PropertyName = "last")]
        public string Last { get; set; }
    }
}