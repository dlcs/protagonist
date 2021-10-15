using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof (StoragePolicyClass),
        Description = "A resource that acts as configuration for a customer or space. It is linked to from " +
                      "the storage resource for any customer or space. ",
        UriTemplate = "/storagePolicies/{0}")]
    public class StoragePolicy : DlcsResource
    {
        [JsonIgnore]
        public string? ModelId { get; set; }

        public StoragePolicy()
        {
        }
        
        public StoragePolicy(string baseUrl, string storagePolicyId)
        {
            ModelId = storagePolicyId;
            Init(baseUrl, true, storagePolicyId);
        }


        [RdfProperty(Description = "The maximum number of images that can be registered, across ALL the Customer's spaces",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "maximumNumberOfStoredImages")]
        public long MaximumNumberOfStoredImages { get; set; }

        [RdfProperty(Description = "The DLCS requires storage capacity to service the images registred by customers. This setting" +
                                   " governs how much capacity the DLCS can use for a Customer across all the customer's spaces. " +
                                   "Capacity is affected by image optimsation policy (higher quality = more storage used) and the absolute" +
                                   "size of the images (pixel dimensions).",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "maximumTotalSizeOfStoredImages")]
        public long MaximumTotalSizeOfStoredImages { get; set; }
    }


    public class StoragePolicyClass : Class
    {
        public StoragePolicyClass()
        {
            BootstrapViaReflection(typeof (StoragePolicy));
        }
        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:storagePolicy_", "Storage policy", Id,
                "GET");
        }
    }
}
