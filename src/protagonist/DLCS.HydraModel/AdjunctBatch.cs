using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(AdjunctBatchClass),
    Description = "Represents a submitted batch of adjuncts.",
    UriTemplate = "/customers/{0}/adjunctQueue/batches/{1}")]
public class AdjunctBatch : DlcsResource
{
    [JsonIgnore]
    public int ModelId { get; set; }

    [JsonIgnore]
    public int CustomerId { get; set; }

    public AdjunctBatch()
    {
    }

    public AdjunctBatch(string baseUrl, int customerId, int modelId)
    {
        ModelId = modelId;
        CustomerId = customerId;
        Init(baseUrl, true, customerId, ModelId);
    }

    [RdfProperty(Description = "Date the batch was POSTed to the adjunct queue",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "submitted")]
    public DateTime Submitted { get; set; }

    [RdfProperty(Description = "Total number of adjuncts in the batch",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "count")]
    public int Count { get; set; }

    [RdfProperty(Description = "Total number of completed adjuncts in the batch",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "completed")]
    public int Completed { get; set; }

    [RdfProperty(Description = "Total number of errored adjuncts in the batch",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "errors")]
    public int Errors { get; set; }

    [RdfProperty(Description = "Date the batch was finished, if it has finished",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "finished")]
    public DateTime? Finished { get; set; }
    
    [HydraLink(Description = "Collection of adjuncts currently claimed by this batch",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 20, PropertyName = "currentAdjuncts")]
    public string? CurrentAdjuncts { get; set; }

    [HydraLink(Description = "All adjuncts historically associated with this batch",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 21, PropertyName = "adjuncts")]
    public string? Adjuncts { get; set; }
}

public class AdjunctBatchClass : Class
{
    public AdjunctBatchClass()
    {
        BootstrapViaReflection(typeof(AdjunctBatch));
    }

    public override void DefineOperations()
    {
        string operationId = "_:customer_adjunctQueue_batch_";
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "AdjunctBatch", Id,
            "GET");

        // These collections are read only

        GetHydraLinkProperty("currentAdjuncts").SupportedOperations = new[]
        {
            CommonOperations.StandardCollectionGet(
                operationId + "current_collection_retrieve",
                "Retrieves the adjuncts currently claimed by this batch",
                null)
        };

        GetHydraLinkProperty("adjuncts").SupportedOperations = new[]
        {
            CommonOperations.StandardCollectionGet(
                operationId + "adjunct_collection_retrieve",
                "Retrieves all adjuncts historically associated with this batch",
                null)
        };
    }
}
