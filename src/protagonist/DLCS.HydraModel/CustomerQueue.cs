using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(CustomerQueueClass),
    Description = "The customer queue resource represents an overview of outstanding images and batches.",
    UriTemplate = "/customers/{0}/queue")]
public class CustomerQueue : DlcsResource
{
    public CustomerQueue()
    {
    }

    public CustomerQueue(string baseUrl, int customerId)
    {
        CustomerId = customerId;
        Init(baseUrl, true, CustomerId);
    }
    
    [JsonIgnore]
    public int CustomerId { get; set; }
    
    [RdfProperty(Description = "Number of total images in your queue, across all batches",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "size")]
    public int Size { get; set; }
    
    [RdfProperty(Description = "Number of total unfinished batches that have not been superseded in your queue",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "batchesWaiting")]
    public long BatchesWaiting { get; set; }
    
    [RdfProperty(Description = "Number of total unfinished images that have not been superseded in your queue, " +
                                "across all batches",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "imagesWaiting")]
    public long ImagesWaiting { get; set; }
    
    // Hydra Link properties
    [HydraLink(Description = "All batches for customer", Range = "vocab:Batch", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "batches")]
    public string? Batches { get; set; }
    
    [HydraLink(Description = "All images for customer", Range = "vocab:Image", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 21, PropertyName = "images")]
    public string? Images { get; set; }
    
    [HydraLink(Description = "All active batches for customer", Range = "vocab:Queue", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 22, PropertyName = "active")]
    public string? Active { get; set; }
    
    [HydraLink(Description = "All recent batches for customer", Range = "vocab:Queue", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 23, PropertyName = "recent")]
    public string? Recent { get; set; }

    [HydraLink(Description = "All recent priority batches for customer", Range = "vocab:Queue", ReadOnly = true,
        WriteOnly = false)]
    [JsonProperty(Order = 24, PropertyName = "priority")]
    public string? Priority { get; set; }
}

public class CustomerQueueClass : Class
{
    const string operationId = "_:customer_queue_";
    
    public CustomerQueueClass()
    {
        BootstrapViaReflection(typeof(CustomerQueue));
    }
    
    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "CustomerQueue", Id, "GET", "POST");
        
        GetHydraLinkProperty("batches").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_queue_batch_collection_retrieve",
                Method = "GET",
                Label = "Retrieves all batches for customer",
                Returns = Names.Hydra.Collection
            }
        };

        GetHydraLinkProperty("recent").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_queue_recent_collection_retrieve",
                Method = "GET",
                Label = "Retrieves the recent (non superseded) batches for customer.",
                Returns = Names.Hydra.Collection
            }
        };


        GetHydraLinkProperty("active").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_queue_active_collection_retrieve",
                Method = "GET",
                Label = "Retrieves the customer's currently running batches.",
                Returns = Names.Hydra.Collection
            }
        };

        GetHydraLinkProperty("images").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_queue_batch_collection_retrieve",
                Method = "GET",
                Label = "Retrieves all images across batches for customer",
                Returns = Names.Hydra.Collection
            }
        };
        
        GetHydraLinkProperty("priority").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_queue_priority_collection_retrieve",
                Method = "GET",
                Label = "Retrieves the priority batches for customer.",
                Returns = Names.Hydra.Collection
            }
        };
    }
}