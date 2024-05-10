using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(CustomerClass), 
    Description = "A customer represents you, the API user. You only have access to one customer, " +
                  "so it is your effective entry point for the API. The only interaction you can have with " +
                  "your Customer resource directly is updating the display name, but it provides links (🔗) to" +
                  "collections of all the other resources.",
    UriTemplate = "/customers/{0}")]
public class Customer : DlcsResource
{
    public enum ReservedNames { Admin }
    
    [JsonIgnore]
    public int ModelId { get; set; }

    public Customer()
    {
    }

    public Customer(string baseUrl, int customerId, bool setLinks)
    {
        ModelId = customerId;
        Init(baseUrl, setLinks, customerId);
    }

    public Customer(string baseUrl, int customerId, string name, string displayName)
    {
        ModelId = customerId;
        Init(baseUrl, true, customerId);
        Name = name;
        DisplayName = displayName;
    }
    
    [RdfProperty(Description = "The URL-friendly name of the customer", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "name")]
    public string? Name { get; set; }

    [RdfProperty(Description = "The display name of the customer",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "displayName")]
    public string? DisplayName { get; set; }


    // Hydra link properties - i.e., a link to another resource, rather than a field of the current resource.
    [HydraLink(Description = "Collection of user accounts that can log into the portal. Use this to grant access to others in your organisation",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "portalUsers")]
    public string? PortalUsers { get; set; }
    
    [HydraLink(Description = "Collection of all the Named Queries you have configured (plus those provided 'out of the box'). " +
                             "See the NamedQuery topic for further information",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "namedQueries")]
    public string? NamedQueries { get; set; }

    [HydraLink(Description = "Collection of configuration settings for retrieving your registered images from their origin URLs. If your" +
                             " images come from multiple locations you will have multiple origin strategies. See the OriginStrategy topic.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "originStrategies")]
    public string? OriginStrategies { get; set; }
    
    [HydraLink(Description = "Collection of further collections containing your delivery channel policies. Policies will be" +
                             " organised by their intended delivery channel.", 
    Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 16, PropertyName = "deliveryChannelPolicies")]
    public string? DeliveryChannelPolicies { get; set; }
    
    [HydraLink(Description = "Collection of default delivery channels. Assets without any delivery channels specified will be served by those" +
                             " configured here (unless overriden by the containing space's default delivery channels). See the DeliveryChannels topic.",
    Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 17, PropertyName = "defaultDeliveryChannels")]
    public string? DefaultDeliveryChannels { get; set; }
    
    [HydraLink(Description = "Collection of IIIF Authentication services available for use with your images. The images are" +
                             " associated with the auth services via Roles. An AuthService is a means of acquiring a role.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 18, PropertyName = "authServices")]
    public string? AuthServices { get; set; }
    
    [HydraLink(Description = "Collection of external services which provide aq login page (typically) and an endpoint " +
                             " from which the DLCS acquires the user's named roles. This enables integration with external auth mechanisms.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 19, PropertyName = "roleProviders")]
    public string? RoleProviders { get; set; }
    
    [HydraLink(Description = "Collection of the available roles you can assign to your images. In order for a user to see an image, the " +
                             "user must have the role associated with the image, or one of them. Users interact with an AuthService to " +
                             "acquire a role or roles.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "roles")]
    public string? Roles { get; set; }

    [HydraLink(Description = "The Customer's view on the DLCS ingest queue. As well as allowing you to query the status of batches you " +
                             "have registered, you can POST new batches to the queue.",
        Range = "vocab:Queue", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 21, PropertyName = "queue")]
    public string? Queue { get; set; }

    [HydraLink(Description = "Collection of all the Space resources associated with your customer. A space allows you to " +
                             "partition images, have different default roles and tags, etc. See the Space topic.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 22, PropertyName = "spaces")]
    public string? Spaces { get; set; }

    [HydraLink(Description = "A paged collection of all the customer's images, regardless of space",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 23, PropertyName = "allImages")]
    public string? AllImages { get; set; }

    [HydraLink(Description = "Storage policy for the Customer",
        Range = "vocab:CustomerStorage", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 24, PropertyName = "storage")]
    public string? Storage { get; set; }

    [HydraLink(Description = "Api keys allocated to this customer. The accompanying secret is only available at creation time. " +
                             "To obtain a key and a secret, make an empty POST to this collection with administrator privileges and the returned " +
                             "Key object will include the generates secret.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 25, PropertyName = "keys")]
    public string? Keys { get; set; }
    
    [HydraLink(Description = "Additional HTTP headers (e.g., for caching) that will be sent for assets that match a role.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 26, PropertyName = "customHeaders")]
    public string? CustomHeaders { get; set; }

    [RdfProperty(Description = "Is this user the admin customer?",
        Range = Names.XmlSchema.Boolean, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 33, PropertyName = "administrator ")]
    public bool? Administrator { get; set; }
    
    [RdfProperty(Description = "Datetime this customer was created.",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 34, PropertyName = "created ")]
    public DateTime? Created { get; set; }

    [RdfProperty(Description = "Has the customer accepted the EULA?",
        Range = Names.XmlSchema.Boolean, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 35, PropertyName = "acceptedAgreement ")]
    public bool? AcceptedAgreement { get; set; }
}

public class CustomerClass : Class
{
    public CustomerClass()
    {
        BootstrapViaReflection(typeof (Customer));
    }

    public override void DefineOperations()
    {
        string operationId = "_:customer_";

        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "Customer", Id,
            "GET");

        // Hydra link properties - i.e., a link to another resource, rather than a field of the current resource.

        GetHydraLinkProperty("portalUsers").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "portalUser_", "Portal User", "vocab:PortalUser");

        GetHydraLinkProperty("namedQueries").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "namedQuery_", "Named Query", "vocab:NamedQuery");

        GetHydraLinkProperty("originStrategies").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "originStrategy_", "Origin Strategy", "vocab:OriginStrategy");

        GetHydraLinkProperty("authServices").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "authService_", "Auth Service", "vocab:AuthService");

        GetHydraLinkProperty("roles").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "role_", "Role", "vocab:Role");

        GetHydraLinkProperty("queue").SupportedOperations = QueueClass.GetSpecialQueueOperations();

        GetHydraLinkProperty("spaces").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "space_", "Space", "vocab:Space");

        
        var images = GetHydraLinkProperty("allImages");
        images.SupportedOperations = CommonOperations
            .GetStandardCollectionOperations("_:customer_image_", "Image", "vocab:Image");
        images.SupportedOperations.WithMethod("GET").Description =
            "Can take query parameters";
        images.SupportedOperations.WithMethod("POST").Description =
            "Push an image for immediate processing, asynchronously. Might fail or timeout. This operation is rate-limited.";

    }
}
