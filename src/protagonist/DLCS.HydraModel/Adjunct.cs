using System;
using System.Collections.Generic;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof (AdjunctClass),
    Description = "A file linked to an asset",
    UriTemplate = "/customers/{0}/spaces/{1}/images/{2}/adjuncts/{3}")]
public class Adjunct : DlcsResource
{
    /// <summary>
    /// 0-param ctor used for deserialising
    /// </summary>
    public Adjunct()
    {
    }
    
    public Adjunct(string baseUrl, int customerId, int space, string assetId, string modelId)
    {
        ModelId = modelId;
        Init(baseUrl, true, customerId, space, assetId, ModelId);
    }
    
    [JsonProperty(Order = 3, PropertyName = "@type")]
    public override string? Type { get; set; }
    
    [RdfProperty(Description = "The identifier for the adjunct",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 10, PropertyName = "id")]
    public string? ModelId { get; set; }

    [RdfProperty(Description = "The asset this adjunct is associated with.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "asset")]
    public string? Asset { get; set; }

    [RdfProperty(Description = "The internet content type (or MIME type) of the resource.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "mediaType")]
    public string? MediaType { get; set; }

    [RdfProperty(Description = "How this adjunct is expressed in IIIF presentation.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "iiifLink")]
    public string? IIIFLink { get; set; }

    [RdfProperty(Description = "A schema or named set of functionality available from the resource.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "profile")]
    public string? Profile { get; set; }

    [RdfProperty(Description = "A human readable label, name or title.",
        Range = Names.XmlSchema.Base, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "label")]
    public Dictionary<string, List<string>>? Label { get; set; }

    [RdfProperty(Description = "The language(s) of the content.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 16, PropertyName = "language")]
    public string[]? Language { get; set; }

    [RdfProperty(Description = "A fully-qualified URL external to the platform.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 17, PropertyName = "externalId")]
    public string? ExternalId { get; set; }

    [RdfProperty(Description = "Where this adjunct is hosted publicly.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 18, PropertyName = "publicId")]
    public string? PublicId { get; set; }

    [RdfProperty(Description = "The size in bytes of the adjunct.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 19, PropertyName = "size")]
    public long? Size { get; set; }

    [RdfProperty(Description = "When this adjunct was created.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "created")]
    public DateTime? Created { get; set; }

    [RdfProperty(Description = "When this adjunct last finished processing.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 21, PropertyName = "finished")]
    public DateTime? Finished { get; set; }

    [RdfProperty(Description = "Origin endpoint from where the original adjunct can be acquired (or was acquired).",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 22, PropertyName = "origin")]
    public string? Origin { get; set; }

    [RdfProperty(Description = "The reason why this adjunct exists.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 23, PropertyName = "motivation")]
    public string? Motivation { get; set; }
    
    [RdfProperty(Description = "An additional set of features or functionality this adjunct provides.",
    Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 25, PropertyName = "provides")]
    public string? Provides { get; set; }
    
    [RdfProperty(Description = "Whether the adjunct is currently being ingested.",
        Range = Names.XmlSchema.Boolean, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 26, PropertyName = "ingesting")]
    public bool? Ingesting { get; set; }

    [RdfProperty(Description = "Reported errors with this adjunct.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 27, PropertyName = "error")]
    public string? Error { get; set; }

    [HydraLink(Description = "The AdjunctBatch this adjunct was most recently submitted in, if any.",
        Range = Names.Hydra.Resource, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 26, PropertyName = "batch")]
    public string? Batch { get; set; }
}

public class AdjunctClass : Class
{
    public AdjunctClass()
    {
        BootstrapViaReflection(typeof(Adjunct));
    }

    public override void DefineOperations()
    {
        string operationId = "_:customer_space_image_adjunct_";
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "Adjunct", Id,
            "GET", "PUT", "DELETE");

        GetHydraLinkProperty("batch").SupportedOperations = new[]
        {
            new Operation
            {
                Id = operationId + "batch_retrieve",
                Method = "GET",
                Label = "Retrieves the AdjunctBatch this adjunct was most recently submitted in",
                Returns = "vocab:AdjunctBatch"
            }
        };
    }
}
