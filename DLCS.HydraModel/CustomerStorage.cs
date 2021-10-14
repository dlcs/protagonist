using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(CustomerStorageClass),
        Description = "Information resource that shows the current storage use for a Customer or for an" +
                      "individual Space within a customer.",
        UriTemplate = "/customers/{0}/storage, /customers/{0}/spaces/{1}/storage")]
    public class CustomerStorage : DlcsResource
    {
        [JsonIgnore]
        public int CustomerId { get; set; }

        [JsonIgnore]
        public int? SpaceId { get; set; }

        public CustomerStorage(string baseUrl, int customerId, int? spaceId)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            if (spaceId.HasValue)
            {
                Init(baseUrl, false, customerId, spaceId);
            }
            else
            {
                Init(baseUrl, false, customerId);
            }
        }

        public CustomerStorage(int customerId, int? spaceId, string storagePolicy,
            long numberOfStoredImages, long totalSizeOfStoredImages, long totalSizeOfThumbnails,
            DateTime lastCalculated)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            StoragePolicy = storagePolicy;
            NumberOfStoredImages = numberOfStoredImages;
            TotalSizeOfStoredImages = totalSizeOfStoredImages;
            TotalSizeOfThumbnails = totalSizeOfThumbnails;
            LastCalculated = lastCalculated;
        }

        [RdfProperty(Description = "Number of stored images",
            Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 20, PropertyName = "numberOfStoredImages")]
        public long? NumberOfStoredImages { get; set; }

        [RdfProperty(Description = "Total storage usage for images excluding thumbnails, in bytes",
            Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 21, PropertyName = "totalSizeOfStoredImages")]
        public long? TotalSizeOfStoredImages { get; set; }

        [RdfProperty(Description = "Total storage usage for thumbnails, in bytes",
            Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 22, PropertyName = "totalSizeOfThumbnails")]
        public long? TotalSizeOfThumbnails { get; set; }

        [RdfProperty(Description = "When the DLCS last evaluated storage use to generate this resource",
            Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 31, PropertyName = "lastCalculated")]
        public DateTime? LastCalculated { get; set; }

        [HydraLink(Description = "When the customer storage resource is for a Customer rather than a space, it" +
                                 "will include this property which configures the total storage permitted across all " +
                                 "a Customer's spaces. ",
            Range = "vocab:StoragePolicy", ReadOnly = true, WriteOnly = false, SetManually = true)]
        [JsonProperty(Order = 81, PropertyName = "storagePolicy")]
        public string? StoragePolicy { get; set; }
    }


    public class CustomerStorageClass : Class
    {
        string operationId = "_:customer_space_storage_";

        public CustomerStorageClass()
        {
            BootstrapViaReflection(typeof (CustomerStorage));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                operationId, "CustomerStorage", Id, "GET");
        }
    }


}
