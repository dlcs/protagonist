using Newtonsoft.Json;

namespace API.Client.JsonLd
{
    public class HydraCollectionBase : JsonLdBase
    {
        public override string Type
        {
            get { return "Collection"; }
        }

        [JsonProperty(Order = 10, PropertyName = "totalItems")]
        public int? TotalItems { get; set; }

        [JsonProperty(Order = 11, PropertyName = "pageSize")]
        public int? PageSize { get; set; }

        [JsonProperty(Order = 90, PropertyName = "view")]
        public PartialCollectionView View { get; set; }

    }
}