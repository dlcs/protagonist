using System;
using DLCS.HydraModel.Converters;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(ImageClass),
    Description = "The Image resource is the DLCS view of an image that you have registered. The job of the DLCS is to offer services on that image, " +
                  "such as IIIF Image API endpoints. As well as the status of the image, the DLCS lets you store arbitrary metadata that you can " +
                  "use to build interesting applications.",
    UriTemplate = "/customers/{0}/spaces/{1}/images/{2}")]
public class Image : DlcsResource
{
    public Image()
    {
    }
    
    public Image(string baseUrl, int customerId, int space, string modelId)
    {
        CustomerId = customerId;
        Space = space;
        ModelId = modelId;
        Init(baseUrl, true, customerId, space, ModelId);
    }
    
    [JsonIgnore]
    public int CustomerId { get; set; }
    
    [JsonIgnore]
    public string? StorageIdentifier { get; set; }

    [RdfProperty(Description = "The identifier for the image within the space - its URI component.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 10, PropertyName = "id")]
    public string? ModelId { get; set; }
    
    [JsonProperty(Order = 10, PropertyName = "space")]
    public int Space { get; set; }

    [RdfProperty(Description = "image service URI - where the IIIF Image API is exposed for this image",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "imageService")]
    public string? ImageService  { get; set; }
    

    [RdfProperty(Description = "Degraded info.json URI - if a user does not have permission to view the full image, " +
                               "but a degraded image is permitted, the DLCS will redirect them to this URI.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "degradedInfoJson")]
    public string? DegradedInfoJson { get; set; }
    
    [RdfProperty(Description = "Thumbnail image service URI",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "thumbnailImageService")]
    public string? ThumbnailImageService { get; set; }

    [RdfProperty(Description = "Direct URI of the 400 pixel thumbnail",
        Range = Names.XmlSchema.String, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "thumbnail400")]
    public string? Thumbnail400 { get; set; }
    
    [RdfProperty(Description = "Date the image was added",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "created")]
    public DateTime? Created { get; set; }
    
    [RdfProperty(Description = "Origin endpoint from where the original image can be acquired (or was acquired)",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "origin")]
    public string? Origin { get; set; }

    [RdfProperty(Description = "Maximum size of request allowed before roles are enforced " +
                               "- relates to the effective WHOLE image size, not the individual tile size." +
                               " 0 = No open option, -1 (default) = no authorisation",
        Range = Names.XmlSchema.Integer, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 17, PropertyName = "maxUnauthorised")]
    public int? MaxUnauthorised { get; set; }
    
    [RdfProperty(Description = "When the image was added to the queue",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 30, PropertyName = "queued")]
    public DateTime? Queued { get; set; }

    [RdfProperty(Description = "When the image was taken off the queue",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 31, PropertyName = "dequeued")]
    public DateTime? Dequeued { get; set; }
    
    [RdfProperty(Description = "When the image processing finished (image ready)",
        Range = Names.XmlSchema.DateTime, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 32, PropertyName = "finished")]
    public DateTime? Finished { get; set; }
    
    [RdfProperty(Description = "Is the image currently being ingested?",
        Range = Names.XmlSchema.Boolean, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 33, PropertyName = "ingesting")]
    public bool? Ingesting { get; set; }

    [RdfProperty(Description = "Reported errors with this image",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 40, PropertyName = "error")]
    public string? Error { get; set; }
    
    // metadata

    [RdfProperty(Description = "Image tags",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 40, PropertyName = "tags")]
    public string[]? Tags { get; set; }
    
    [RdfProperty(Description = "String reference 1",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 41, PropertyName = "string1")]
    public string? String1 { get; set; }

    [RdfProperty(Description = "String reference 2",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 42, PropertyName = "string2")]
    public string? String2 { get; set; }

    [RdfProperty(Description = "String reference 3",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 43, PropertyName = "string3")]
    public string? String3 { get; set; }

    [RdfProperty(Description = "Number reference 1",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 51, PropertyName = "number1")]
    public int? Number1 { get; set; }

    [RdfProperty(Description = "Number reference 2",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 52, PropertyName = "number2")]
    public int? Number2 { get; set; }

    [RdfProperty(Description = "Number reference 3",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 53, PropertyName = "number3")]
    public int? Number3 { get; set; }
    
    
    // Asset extents
    [RdfProperty(Description = "Duration of media in milliseconds",
        Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "duration")]
    public long? Duration { get; set; }
    
    [RdfProperty(Description = "Tile source width",
        Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "width")]
    public int? Width { get; set; }
    
    [RdfProperty(Description = "Tile source height",
        Range = Names.XmlSchema.Integer, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "height")]
    public int? Height { get; set; }
    
    // TODO - this is used by Wellcome DDS but is not documented.
    // I think it should be a hydra link property
    [HydraLink(Description = "Processing metadata - used by DDS to learn about transcode output time-based media",
        Range = "vocab:ProcessingMetadata", ReadOnly = true, WriteOnly = false, SetManually = false)]
    [JsonProperty(Order = 82, PropertyName = "metadata")]
    public string? Metadata { get; set; } // herein duration, other stuff learnt during transcoding
    
    [HydraLink(Description = "Storage information - how much space taken up by images and thumbs - link is present on Deliverator API",
        Range = "vocab:AssetStorageInfo", ReadOnly = true, WriteOnly = false, SetManually = false)]
    [JsonProperty(Order = 82, PropertyName = "storage")]
    public string? Storage { get; set; } 
    
    [RdfProperty(Description = "Media Type (content type)",
        Range = Names.XmlSchema.String, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 120, PropertyName = "mediaType")]
    public string? MediaType { get; set; }
    
    [RdfProperty(Description = "Asset Family (I for Image, T for time-based, F for File)",
        Range = Names.XmlSchema.String, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 130, PropertyName = "family")]
    public AssetFamily? Family { get; set; } 
    
    // TODO - Replace this with https://github.com/dlcs/protagonist/issues/148
    [RdfProperty(Description = "URI of a text source for this asset (e.g., OCR data, captions etc",
        Range = Names.XmlSchema.String, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 120, PropertyName = "text")]
    public string? Text { get; set; }
    
    [RdfProperty(Description = "Media type and/or profile to identify the format of the text",
        Range = Names.XmlSchema.String, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 130, PropertyName = "textType")]
    public string? TextType { get; set; } // e.g., METS-ALTO, hOCR, TEI, text/plain etc
    
    [RdfProperty(Description = "Delivery channel specifying how the asset will be available.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 140, PropertyName = "deliveryChannels")]
    [JsonConverter(typeof(ImageDeliveryChannelsConverter))]
    public DeliveryChannel[]? DeliveryChannels { get; set; }
    
    [RdfProperty(Description = "The role or roles that a user must possess to view this image above maxUnauthorised. " +
                               "These are URIs of roles e.g., https://api.dlcs.io/customers/1/roles/requiresRegistration",
        Range = "vocab:Role", ReadOnly = false, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 70, PropertyName = "roles")]
    public string[]? Roles { get; set; }

    // Hydra Link properties
    [HydraLink(Description = "The batch this image was ingested in (most recently). Might be blank if the batch has been archived or the image as ingested in immediate mode.",
        Range = "vocab:Batch", ReadOnly = true, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 71, PropertyName = "batch")]
    public string? Batch { get; set; }
    
    [HydraLink(Description = "The image optimisation policy used when this image was last processed (e.g., registered)",
        Range = "vocab:ImageOptimisationPolicy", ReadOnly = true, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 80, PropertyName = "imageOptimisationPolicy")]
    public string? ImageOptimisationPolicy { get; set; }

    [HydraLink(Description = "The thumbnail settings used when this image was last processed (e.g., registered)",
        Range = "vocab:ThumbnailPolicy", ReadOnly = true, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 81, PropertyName = "thumbnailPolicy")]
    public string? ThumbnailPolicy { get; set; }
}

public class ImageClass: Class
{
    string operationId = "_:customer_space_image_";
    public ImageClass()
    {
        BootstrapViaReflection(typeof(Image));
    }
    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            operationId, "Image", Id,
            "GET", "PUT", "PATCH", "DELETE"); // TODO - what is and is not allowed here...?

        GetHydraLinkProperty("roles").SupportedOperations = CommonOperations
            .GetStandardCollectionOperations(operationId + "_role_", "Role", "vocab:Role");

        GetHydraLinkProperty("batch").SupportedOperations = CommonOperations
            .GetStandardResourceOperations(operationId + "_batch", "Batch", Id,
            "GET", "PUT", "PATCH", "DELETE");
    }
}
