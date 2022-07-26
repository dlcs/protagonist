using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof (QueueClass),
    Description = "The Queue resource allows the DLCS to process very large number of image registration requests." +
                  "You can post a Collection of images to the Queue for processing (a Hydra collection, see note). This results " +
                  "in the creation of a Batch resource. You can then retrieve these batches to monitor the progress of your images.",
    UriTemplate = "/customers/{0}/queue")]
public class Queue : DlcsResource
{
    [JsonIgnore]
    public int CustomerId { get; set; }

    public Queue()
    {
    }
    
    public Queue(string baseUrl, int customerId)
    {
        CustomerId = customerId;
        Init(baseUrl, true, customerId);
    }

    [RdfProperty(Description = "Number of total images in your queue, across all batches",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "size")]
    public int Size { get; set; }

    [HydraLink(Description = "Collection (paged) of the batches - the separate jobs you have submitted to the queue",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "batches")]
    public string? Batches { get; set; }

    [HydraLink(Description = "Collection (paged). Merged view of images on the queue, across batches. Typically you'd use this to " +
                             "look at the top or bottom of the queue (first or large page).",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "images")]
    public string? Images { get; set; }


    [HydraLink(Description = "Collection (paged) of finished batches which are not marked as superseded. Most recent first.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "recent")]
    public string? Recent { get; set; }


    [HydraLink(Description = "Collection (paged) of batches that are currently in process.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "active")]
    public string? Active { get; set; }

}


public class QueueClass : Class
{
    public QueueClass()
    {
        BootstrapViaReflection(typeof (Queue));
    }

    public override void DefineOperations()
    {
        SupportedOperations = GetSpecialQueueOperations();

        // you can't POST a batch - you do this by posting Image[] to Queue.
        // Or do you?
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
    }

    public static Operation[] GetSpecialQueueOperations()
    {
        return new[]
        {
            new Operation
            {
                Id = "_:customer_queue_retrieve",
                Method = "GET",
                Label = "Returns the queue resource",
                Returns = "vocab:Queue"
            },
            new Operation
            {
                Id = "_:customer_queue_create_batch",
                Method = "POST",
                Label = "Submit an array of Image and get a batch back",
                Description = "(doc here)",
                // TODO: I want to say Expects: vocab:Image[] - but how do we do that?
                // We've lost information here - how does a client know how to send a collection of images? How do we declare that?
                // When we say that something returns a Collection that's OK, because the client can inspect the members of the collection.
                // but how do we declare that the API user should POST a collection?
                Expects = Names.Hydra.Collection, // 
                // maybe it's something else - like:
                // Expects = "vocab:ImageList"
                // where we define another Class in the documentation, and ImageList just has an Images property []
                // but that is no different from the members of a collection.
                // see http://lists.w3.org/Archives/Public/public-hydra/2016Jan/0087.html
                // From that I'll leave as Collection and rely on out-of-band knowledge until Hydra catches up.
                Returns = "vocab:Batch",
                StatusCodes = new[]
                {
                    new Status
                    {
                        StatusCode = 201,
                        Description = "Job has been accepted - Batch created and returned"
                    }
                }
            }
        };
    } 
}
