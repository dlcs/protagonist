using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(CustomerAdjunctQueueClass),
    Description = "The customer adjunct queue resource represents an overview of outstanding adjuncts and adjunct batches.",
    UriTemplate = "/customers/{0}/adjunctQueue")]
public class CustomerAdjunctQueue : DlcsResource
{
    public CustomerAdjunctQueue()
    {
    }

    public CustomerAdjunctQueue(string baseUrl, int customerId)
    {
        CustomerId = customerId;
        Init(baseUrl, true, CustomerId);
    }

    [JsonIgnore]
    public int CustomerId { get; set; }

    [RdfProperty(Description = "Number of total adjuncts in your queue, across all batches",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "size")]
    public int Size { get; set; }

    [RdfProperty(Description = "Number of total adjunct batches that have not been started in your queue",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "batchesWaiting")]
    public long BatchesWaiting { get; set; }

    [RdfProperty(Description = "Number of adjuncts waiting to be processed. These are adjuncts that have been " +
                                "submitted but the platform has not yet started working on them; excludes " +
                                "adjuncts currently being processed",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "adjunctsWaiting")]
    public long AdjunctsWaiting { get; set; }

    // Hydra Link properties
    [HydraLink(Description = "All adjunct batches for customer", Range = "vocab:AdjunctBatch", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "batches")]
    public string? Batches { get; set; }

    [HydraLink(Description = "All active adjunct batches for customer", Range = "vocab:AdjunctQueue", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 21, PropertyName = "active")]
    public string? Active { get; set; }

    [HydraLink(Description = "All recent adjunct batches for customer", Range = "vocab:AdjunctQueue", ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 22, PropertyName = "recent")]
    public string? Recent { get; set; }
}

public class CustomerAdjunctQueueClass : Class
{
    const string operationId = "_:customer_adjunct_queue_";

    public CustomerAdjunctQueueClass()
    {
        BootstrapViaReflection(typeof(CustomerAdjunctQueue));
    }

    public static Operation[] GetSpecialAdjunctQueueOperations()
    {
        return new[]
        {
            new Operation
            {
                Id = "_:customer_adjunctQueue_retrieve",
                Method = "GET",
                Label = "Returns the adjunct queue resource",
                Returns = "vocab:CustomerAdjunctQueue"
            },
            new Operation
            {
                Id = "_:customer_adjunctQueue_create_batch",
                Method = "POST",
                Label = "Submit a collection of Adjuncts and get an AdjunctBatch back",
                Expects = Names.Hydra.Collection,
                Returns = "vocab:AdjunctBatch",
                StatusCodes = new[]
                {
                    new Status
                    {
                        StatusCode = 201,
                        Description = "AdjunctBatch created and returned"
                    }
                }
            }
        };
    }

    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "CustomerAdjunctQueue", Id, "GET", "POST");

        GetHydraLinkProperty("batches").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_adjunct_queue_batch_collection_retrieve",
                Method = "GET",
                Label = "Retrieves all adjunct batches for customer",
                Returns = Names.Hydra.Collection
            }
        };

        GetHydraLinkProperty("active").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_adjunct_queue_active_collection_retrieve",
                Method = "GET",
                Label = "Retrieves the customer's currently running adjunct batches.",
                Returns = Names.Hydra.Collection
            }
        };

        GetHydraLinkProperty("recent").SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_adjunct_queue_recent_collection_retrieve",
                Method = "GET",
                Label = "Retrieves the recent (finished) adjunct batches for customer.",
                Returns = Names.Hydra.Collection
            }
        };
    }
}
