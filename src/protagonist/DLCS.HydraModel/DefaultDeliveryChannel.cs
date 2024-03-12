using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(DefaultDeliveryChannelClass),
    Description = "Assets that have not been assigned any delivery channels will use any matching " +
                  "default delivery channels configured in the customer or containing space.",
    UriTemplate = "/customers/{0}/defaultDeliveryChannels/{1}, /customers/{0}/spaces/{1}/defaultDeliveryChannels/{2}")]
public class DefaultDeliveryChannel : DlcsResource
{
    public DefaultDeliveryChannel()
    {
    }
    
    public DefaultDeliveryChannel(string baseUrl, int customerId, string channel, string? policy, string mediaType, string id, int space)
    {
        Channel = channel;
        Policy = policy;
        MediaType = mediaType;

        if (space == 0)
        {
            Init(baseUrl, true, customerId, id);
        }
        else
        {
            Init(baseUrl, true, customerId, space, id);
        }
    }
    
    [RdfProperty(Description = "The name of the DLCS delivery channel this is based on.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "channel")]
    public string? Channel { get; set; }
    
    [HydraLink(Description = "The policy assigned to this default delivery channel.",
        Range = "vocab:deliveryChannelPolicy", ReadOnly = false, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 12, PropertyName = "policy")]
    public string? Policy { get; set; }
    
    [HydraLink(Description = "The asset media type matched by this default delivery channel.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 12, PropertyName = "mediaType")]
    public string? MediaType { get; set; }
}

public class DefaultDeliveryChannelClass: Class
{
    string operationId = "_:defaultDeliveryChannel_";
    
    public DefaultDeliveryChannelClass()
    {
        BootstrapViaReflection(typeof(DefaultDeliveryChannel));
    }
    
    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "Default Delivery Channel", Id,
            "GET", "POST", "PUT", "DELETE");
    }
}