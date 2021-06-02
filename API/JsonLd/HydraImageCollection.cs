using Newtonsoft.Json;

namespace API.JsonLd
{
    public class HydraImageCollection : HydraCollectionBase
    {
        [JsonProperty(Order = 20, PropertyName = "member")] 
        public Image[] Members { get; set; }
    }
}