using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(DeliveryChannelClass),
    Description = "A delivery channel represents a way an asset on the DLCS can be served.",
    UriTemplate = "")]
public class DeliveryChannel : DlcsResource
{
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
            "GET", "POST", "PUT");
    }
}