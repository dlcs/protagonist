using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(ImageOptimisationPolicyClass),
   Description = "An internal record of how the DLCS optimised your image for tile delivery. Provides " +
                 "A URI to identify which policy was used at registration time for each of your images. " +
                 "This will be needed if you ever want to re-register from origin (e.g., go for a higher " +
                 "or lower quality, etc).",
   UriTemplate = "/imageOptimisationPolicies/{0}")]
public class ImageOptimisationPolicy : DlcsResource
{
    [JsonIgnore]
    public string? ModelId { get; set; }

    public ImageOptimisationPolicy()
    {
    }

    public ImageOptimisationPolicy(string baseUrl, string imageOptimisationPolicyId, string name, string technicalDetails)
    {
        ModelId = imageOptimisationPolicyId;
        Init(baseUrl, true, imageOptimisationPolicyId);
        Name = name;
        TechnicalDetails = technicalDetails;
    }

    [RdfProperty(Description = "The human readable name of the image policy",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "name")]
    public string? Name { get; set; }

    [RdfProperty(Description = "Details of the encoding and tools used. Might not be public.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "technicalDetails")]
    public string? TechnicalDetails { get; set; }
}


public class ImageOptimisationPolicyClass : Class
{
    public ImageOptimisationPolicyClass()
    {
        BootstrapViaReflection(typeof(ImageOptimisationPolicy));
    }

    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            "_:imageOptimisationPolicy_", "Image optimisation policy", Id,
            "GET");
    }
}
