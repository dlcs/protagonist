using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(ThumbnailPolicyClass),
        Description = "The settings used to create thumbnails for the image at registration time.",
        UriTemplate = "/thumbnailPolicies/{0}")]
    public class ThumbnailPolicy : DlcsResource
    {
        [JsonIgnore]
        public string ModelId { get; set; }


        public ThumbnailPolicy(string baseUrl, string thumbnailPolicyId)
        {
            ModelId = thumbnailPolicyId;
            Init(baseUrl, true, thumbnailPolicyId);
        }


        [RdfProperty(Description = "The human readable name of the image policy",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "name")]
        public string? Name { get; set; }

        [RdfProperty(Description = "The bounding box size of the thumbnails to create. For each of these sizes, a thumbnail " +
                                   "will be created. The longest edge of each thumbnail matches this size.",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "sizes")]
        public int[]? Sizes { get; set; }
    }


    public class ThumbnailPolicyClass : Class
    {
        public ThumbnailPolicyClass()
        {
            BootstrapViaReflection(typeof(ThumbnailPolicy));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:thumbnailPolicy_", "Thumbnail policy", Id,
                "GET");
        }
    }
}