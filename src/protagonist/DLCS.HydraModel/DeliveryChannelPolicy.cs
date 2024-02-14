using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof (DeliveryChannelPolicyClass),
    Description = "A policy for a delivery channel.",
    UriTemplate = "/customers/{0}/deliveryChannelPolicies/{1}/{2}")]
public class DeliveryChannelPolicy : DlcsResource
{
    [JsonIgnore]
    public int CustomerId { get; set; }
    
    public DeliveryChannelPolicy()
    {
    }
    
    public DeliveryChannelPolicy(string baseUrl)
    {
        Init(baseUrl, false);
    }
    
    [RdfProperty(Description = "The URL-friendly name of this delivery channel policy.", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 10, PropertyName = "name")]
    public string? Name { get; set; }
    
    [RdfProperty(Description = "The display name of this delivery channel policy", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "displayName")]
    public string? DisplayName { get; set; }
    
    [RdfProperty(Description = "The delivery channel this policy is for.", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "channel")]
    public string? Channel { get; set; }   
    
    [RdfProperty(Description = "A JSON object containing configuration for the specified delivery channel - see the DeliveryChannels topic.", 
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "policyData")]
    public string? PolicyData { get; set; }

    [RdfProperty(Description = "The date this policy was created.",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "policyCreated")]
    public DateTime? Created { get; set; }  
    
    [RdfProperty(Description = "The date this policy was last modified.",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "policyModified")]
    public DateTime? Modified { get; set; }   
}

public class DeliveryChannelPolicyClass: Class
{
    public DeliveryChannelPolicyClass()
    {
        BootstrapViaReflection(typeof(DeliveryChannelPolicy));
    }

    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            "_:customer_deliveryChannelPolicy_", "Delivery Channel Policy", Id,
            "GET", "POST", "PUT", "PATCH", "DELETE");
    }
}