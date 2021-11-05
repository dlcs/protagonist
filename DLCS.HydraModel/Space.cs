using System;
using System.Linq;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(SpaceClass),
           Description = "Spaces allow you to partition images into groups. You can use them to organise your " +
                         "images logically, like folders. You can also define different default settings to apply " +
                         "to images registered in a space. For example, default access control behaviour for all " +
                         "images in a space, or default tags. These can be overridden for individual images. " +
                         "There is no limit to the number of images you can register in a space.",
           UriTemplate = "/customers/{0}/spaces/{1}")]
    [Unstable(Note = "Under active development")]
    public class Space : DlcsResource
    {
        [JsonIgnore]
        public int CustomerId { get; set; }

        public Space()
        {
        }

        public Space(string baseUrl, int modelId, int customerId)
        {
            ModelId = modelId;
            CustomerId = customerId;
            Init(baseUrl, true, customerId, ModelId);
        }

        // Space and Image make ModelId part of the public JSON, as the id property.
        [RdfProperty(Description = "The internal identifier for the space within the customer (uri component)",
            Range = Names.XmlSchema.Integer, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 10, PropertyName = "id")]
        public int? ModelId { get; set; }

        [RdfProperty(Description = "Space name",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "name")]
        public string? Name { get; set; }

        [RdfProperty(Description = "Date the space was created",
            Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "created")]
        public DateTime? Created { get; set; }

        [RdfProperty(Description = "Default tags to apply to images created in this space",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "defaultTags")]
        public string[]? DefaultTags { get; set; }

        [RdfProperty(Description = "Default size at which role-based authorisation will be enforced. -1=open, 0=always require auth",
            Range = Names.XmlSchema.Integer, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 14, PropertyName = "maxUnauthorised")]
        public int? MaxUnauthorised { get; set; }
        
        
        [RdfProperty(Description = "Computed count of the number of images in the space.",
            Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 14, PropertyName = "approximateNumberOfImages")]
        public long? ApproximateNumberOfImages { get; set; }
        
        

        [HydraLink(Description = "Default roles that will be applied to images in this space",
            Range = Names.Hydra.Collection, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 20, PropertyName = "defaultRoles")]
        public string? DefaultRoles { get; set; }

        [HydraLink(Description = "All the images in the space",
            Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 22, PropertyName = "images")]
        public string? Images { get; set; }

        [HydraLink(Description = "Metadata options for the space", // TOOD- what exactly?
            Range = "vocab:Metadata", ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 24, PropertyName = "metadata")]
        public string? Metadata { get; set; }

        [HydraLink(Description = "Storage policy for the space", 
            Range = "vocab:CustomerStorage", ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 28, PropertyName = "storage")]
        public string? Storage { get; set; }

    }

    public class SpaceClass : Class
    {
        public SpaceClass()
        {
            BootstrapViaReflection(typeof(Space));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:customer_space_", "Space", Id,
                "GET", "PUT", "PATCH", "DELETE");

            var images = GetHydraLinkProperty("images");
            images.SupportedOperations = CommonOperations
                .GetStandardCollectionOperations("_:customer_space_image_", "Image", "vocab:Image");
            images.SupportedOperations.WithMethod("GET").Description =
                "Can take query parameters";
            images.SupportedOperations.WithMethod("POST").Description =
                "Push an image for immediate processing, asynchronously. Might fail or timeout. This operation is rate-limited.";

            GetHydraLinkProperty("defaultRoles").SupportedOperations = CommonOperations
                .GetStandardCollectionOperations("_:customer_space_defaultRole_", "Role", "vocab:Role");

            GetHydraLinkProperty("metadata").SupportedOperations = new []
            {
                new Operation
                {
                    Id = Id,
                    Method = "GET",
                    Label = "Retrieve the metadata",
                    Description = "desc",
                    Returns = "vocab:Metadata",
                    StatusCodes = new[]
                    {
                        new Status
                        {
                            StatusCode = 200,
                            Description = "OK"
                        }
                    }
                }
            };
        }
    }
    
    public static class SpaceX
    {
        public static string ManifestTag = "dlcs:manifestSpace";
        
        /// <summary>
        /// Check 
        /// </summary>
        /// <param name="space"></param>
        /// <returns></returns>
        public static bool IsManifestSpace(this Space space) 
            => space.DefaultTags.Contains(ManifestTag);
        
        
        public static void AddDefaultTag(this Space space, string tag)
        {
            throw new NotImplementedException();
        }
        
        public static void RemoveDefaultTag(this Space space, string tag)
        {
            throw new NotImplementedException();
        }
    }
}
