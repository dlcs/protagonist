using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(ImageStorageClass),
        Description = "Resource that shows how much storage a registered DLCS Image uses. ",
        UriTemplate = "/customers/{0}/spaces/{1}/images/{2}/storage")]
    public class ImageStorage : DlcsResource
    {
        [JsonIgnore]
        public string? ModelId { get; set; }

        [JsonIgnore]
        public int CustomerId { get; set; }

        [JsonIgnore]
        public int SpaceId { get; set; }

        public ImageStorage()
        {
        }

        public ImageStorage(string baseUrl, int customerId, int spaceId, string imageId)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            ModelId = imageId;
            Init(baseUrl, false, customerId, spaceId, imageId);
        }

        [RdfProperty(Description = "Storage space taken up by this item's thumbnails",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 53, PropertyName = "thumbnailSize")]
        public int ThumbnailSize { get; set; }

        [RdfProperty(Description = "Storage space taken up by the DLCS artifacts for this item",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 54, PropertyName = "size")]
        public int Size { get; set; }

        [RdfProperty(Description = "When these figures were last computed",
            Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 55, PropertyName = "lastChecked")]
        public DateTime? LastChecked { get; set; }

        [RdfProperty(Description = "If a computation of these figures is currently running",
            Range = Names.XmlSchema.Boolean, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 55, PropertyName = "checkingInProgress")]
        public bool CheckingInProgress { get; set; }

    }

    public class ImageStorageClass : Class
    {
        public ImageStorageClass()
        {
            BootstrapViaReflection(typeof(ImageStorage));
        }
        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:customer_space_image_storage", "Image Storage", Id,
                "GET");
        }

    }
}
