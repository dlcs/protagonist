using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(CustomHeaderClass), 
    Description = "The DLCS can add additional HTTP headers into asset responses (e.g., image tiles) on a role-by-role basis." + 
                  "For example, you could add a private cache-control header to an access-controlled image. This is done on a" +
                  "role basis - each CustomHeader resource has a role associated with it, and the header will only be added " +
                  "if the asset has this role. To add headers to assets with no roles, create a Custom Header without a role property.",
    UriTemplate = "/customers/{0}/customHeaders/{1}")]
public class CustomHeader : DlcsResource
{
    [JsonIgnore]
    public string? ModelId { get; set; }
    
    [JsonIgnore]
    public int CustomerId { get; set; }
    
    public CustomHeader()
    {
    }

    public CustomHeader(string baseUrl, int customerId, string customHeaderId, bool setLinks)
    {
        CustomerId = customerId;
        ModelId = customHeaderId;
        Init(baseUrl, setLinks, customHeaderId);
    }
    
    [HydraLink(Description = "URI of the registered DLCS role that assets must have for this HTTP header to be set. " +
                             "Leave blank to set headers for assets with no roles.",
        Range = "vocab:Role", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "role")]
    public string? Role { get; set; }
    
    [RdfProperty(Description = "The valid name of the HTTP Header (e.g., 'Cache-Control')", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "key")]
    public string? Key { get; set; }

    [RdfProperty(Description = "The value of the HTTP Header", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 16, PropertyName = "value")]
    public string? Value { get; set; }
    
    [RdfProperty(Description = "The ID of the space this HTTP header is assigned to",
        Range = Names.XmlSchema.Integer, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 17, PropertyName = "spaceId")]
    public int? SpaceId { get; set; }
}

public class CustomHeaderClass : Class
{
    public CustomHeaderClass()
    {
        BootstrapViaReflection(typeof (CustomHeader));
    }

    public override void DefineOperations()
    {
        string operationId = "_:customHeader_";

        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "CustomHeader", Id,
            "GET");
    }
}