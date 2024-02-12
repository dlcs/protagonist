using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(DeliveryChannelClass),
    Description = "A delivery channel represents a way an asset on the DLCS can be served.",
    UriTemplate = "/customers/{0}/defaultDeliveryChannels/{1}, /customers/{0}/spaces/{1}/defaultDeliveryChannels/{2}")]
public class DeliveryChannel : DlcsResource
{
    public DeliveryChannel()
    {
        
    }
    
    public DeliveryChannel(string baseUrl, int customerId, string deliveryChannelId, int? spaceId)
    {
        ModelId = deliveryChannelId;
        CustomerId = customerId;
        SpaceId = spaceId;
        if (spaceId.HasValue)
        {
            Init(baseUrl, false, customerId, spaceId, deliveryChannelId);
        }
        else
        {
            Init(baseUrl, false, customerId, deliveryChannelId);
        }
    }
    
    [JsonIgnore]
    public int CustomerId { get; set; }
    
    [JsonIgnore]
    public int? SpaceId { get; set; }
    
    [JsonIgnore]
    public string? ModelId { get; set; }
    
    [RdfProperty(Description = "The name of the DLCS delivery channel this is based on.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "channel")]
    public string? Channel { get; set; }
    
    [HydraLink(Description = "The policy assigned to this delivery channel.",
        Range = "vocab:deliveryChannelPolicy", ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "policy")]
    public string? Policy { get; set; }
}

public class DeliveryChannelClass: Class
{
    string operationId = "_:deliveryChannel_";
    
    public DeliveryChannelClass()
    {
        BootstrapViaReflection(typeof(DeliveryChannel));
    }
    
    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "Delivery Channel", Id,
            "GET", "PUT", "DELETE");
    }
}