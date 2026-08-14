using System;
using System.Linq;
using DLCS.Core.Collections;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(SpaceClass),
       Description = "Spaces allow you to partition images into groups. You can use them to organise your " +
                     "images logically, like folders. You can also define different default settings to apply " +
                     "to images registered in a space, for example default tags. " +
                     "These can be overridden for individual images. " +
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

    [RdfProperty(Description = "Computed count of the number of images in the space.",
        Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "approximateNumberOfImages")]
    public long? ApproximateNumberOfImages { get; set; }
    
    [RdfProperty(Description = "Default roles that will be applied to images in this space",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "defaultRoles")]
    public string[]? DefaultRoles { get; set; }

    [HydraLink(Description = "All the images in the space",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 22, PropertyName = "images")]
    public string? Images { get; set; }

    [HydraLink(Description = "Collection of default delivery channels. Assets without any delivery channels specified will be served by those" +
                             " configured here. See the DeliveryChannels topic.",
    Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 23, PropertyName = "defaultDeliveryChannels")]
    public string? DefaultDeliveryChannels { get; set; }
    
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
        images.SupportedOperations = new[]
        {
            CommonOperations.StandardCollectionGet(
                "_:customer_space_image_collection_retrieve", "Retrieves all Images", "Can take query parameters"),
            new Operation
            {
                Id = "_:customer_space_image_bulk_update",
                Method = "PATCH",
                Label = "Update one or more images in the space",
                Description = "Each image in the supplied collection must have an id, and may only " +
                              "set fields that do not require the asset to be reprocessed.",
                Expects = Names.Hydra.Collection,
                Returns = Names.Hydra.Collection,
                StatusCodes = new[]
                {
                    new Status { StatusCode = 200, Description = "OK" },
                    new Status { StatusCode = 400, Description = "Bad Request" }
                }
            }
        };

        GetHydraLinkProperty("defaultRoles").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations("_:customer_space_defaultRole_", "Role", "vocab:Role");
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
        space.DefaultTags = StringArrays.EnsureString(space.DefaultTags, tag);
    }
    
    public static void RemoveDefaultTag(this Space space, string tag)
    {
        space.DefaultTags = StringArrays.RemoveString(space.DefaultTags, tag);
    }
}
