using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(BatchClass),
           Description = "Represents a submitted job of images. Typically you'd interact with this while it is being processed, " +
                         "or to update your internal systems with the status of images on the DLCS." +
                         " The DLCS might clear out old batches after a specific time interval.",
           UriTemplate = "/customers/{0}/queue/batches/{1}")]
    public class Batch : DlcsResource
    {
        [JsonIgnore]
        public int ModelId { get; set; }
        [JsonIgnore]
        public int CustomerId { get; set; }

        public Batch() { }

        public Batch(string baseUrl, int modelId, int customerId, DateTime submitted)
        {

            ModelId = modelId;
            CustomerId = customerId;
            Submitted = submitted;
            //Count = count;
            //Finished = finished;
            //Errors = errors;
            //EstCompletion = estCompletion;
            Init(baseUrl, true, customerId, ModelId);
        }


        [RdfProperty(Description = "Date the batch was POSTed to the queue",
            Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "submitted")]
        public DateTime Submitted { get; set; }

        [RdfProperty(Description = "Total number of images in the batch",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "count")]
        public int Count { get; set; }
        
        [RdfProperty(Description = "Total number of completed images in the batch",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "completed")]
        public int Completed { get; set; }

        [RdfProperty(Description = "Date the batch was finished, if it has finished (may still have errors)",
            Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 13, PropertyName = "finished")]
        public DateTime? Finished { get; set; }

        [RdfProperty(Description = "Total number of error images in the batch",
            Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 14, PropertyName = "errors")]
        public int Errors { get; set; }

        [RdfProperty(Description = "Has this batch been superseded by another? An image can only be associated with one active batch at a time. " +
                                   "If no images are associated with this batch, then it has been superseded by one or more later batches. The DLCS does not" +
                                   "update this property automatically, you can force an update by POSTing to the /test resource of a batch.",
            Range = Names.XmlSchema.Boolean, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 14, PropertyName = "superseded")]
        public bool Superseded { get; set; }

        [RdfProperty(Description = "Estimated Completion (best guess as to when this batch might be finished)",
            Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 15, PropertyName = "estCompletion")]
        public DateTime? EstCompletion { get; set; }

        [HydraLink(Description = "Collection of all the images in the batch",
            Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 20, PropertyName = "images")]
        public string Images { get; set; }

        [HydraLink(Description = "Collection of images that have completed processing",
            Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 20, PropertyName = "completedImages")]
        public string CompletedImages { get; set; }

        [HydraLink(Description = "Collection of images that encountered errors",
            Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 20, PropertyName = "errorImages")]
        public string ErrorImages { get; set; }

        [HydraLink(Description = "POST to this to force an update of the batch's superseded property. " +
                                 "Returns JSON object with single success property (boolean). ",
            Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 22, PropertyName = "test")]
        public string Test { get; set; }
    }

    public class BatchClass : Class
    {
        public BatchClass()
        {
            BootstrapViaReflection(typeof(Batch));
        }

        public override void DefineOperations()
        {
            string operationId = "_:customer_queue_batch_";
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                operationId, "Batch", Id,
                "GET"); // do we allow DELETE?

            // These collections are read only

            GetHydraLinkProperty("images").SupportedOperations = new[]
            {
                CommonOperations.StandardCollectionGet(
                    operationId + "image_collection_retrieve",
                    "Retrieves all images in batch regardless of state",
                    null)
            };

            GetHydraLinkProperty("completedImages").SupportedOperations = new[]
            {
                CommonOperations.StandardCollectionGet(
                    operationId + "completedImage_collection_retrieve",
                    "Retrieves all COMPLETED images in batch",
                    null)
            };

            GetHydraLinkProperty("errorImages").SupportedOperations = new[]
            {
                CommonOperations.StandardCollectionGet(
                    operationId + "errorImage_collection_retrieve",
                    "Retrieves all ERROR images in batch",
                    null)
            };
        }
    }
}
