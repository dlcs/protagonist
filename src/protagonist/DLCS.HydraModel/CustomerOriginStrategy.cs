using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(CustomerOriginStrategyClass),
    Description = "As a customer you can provide information to the DLCS to allow it to fetch your images " +
                  "from their origin endpoints. Every customer has a default origin strategy, which is for the " +
                  "DLCS to attempt to fetch the image from its origin URL without presenting credentials. " +
                  "This is fine for images that are publicly available, but is unlikely to be appropriate for " +
                  "images you are exposing from your asset management system. You might have a service that is " +
                  "available only to the DLCS, or an FTP site. The DLCS has a predefined set of mechanisms for " +
                  "obtaining resources over HTTP, FTP, S3 etc. In your customer origin strategies you match these predefined strategies " +
                  "to regexes that match your origin URLs and credentials that the DLCS can use when requesting " +
                  "your assets.",
    UriTemplate = "/customers/{0}/originStrategies/{1}")]
public class CustomerOriginStrategy : DlcsResource
{
    [JsonIgnore]
    public int ModelId { get; set; }
    [JsonIgnore]
    public int CustomerId { get; set; }

    public CustomerOriginStrategy()
    {
    }
    
    public CustomerOriginStrategy(string baseUrl, int customerId, int strategyId)
    {
        CustomerId = customerId;
        ModelId = strategyId;
        Init(baseUrl, true, customerId, ModelId);
    }

    
    [RdfProperty(Description = "Regex for matching origin. When the DLCS tries to work out how to fetch " +
                               "from your origin, it uses this regex to match to find the correct strategy.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "regex")]
    public string? Regex { get; set; }


    [HydraLink(Description = "Link to the origin strategy definition that will be used if the regex is matched.",
        Range = "vocab:OriginStrategy", ReadOnly = true, WriteOnly = false, SetManually = true)]
    [JsonProperty(Order = 17, PropertyName = "originStrategy")]
    public string? OriginStrategy { get; set; }


    [HydraLink(Description = "JSON object - credentials appropriate to the protocol, will vary. " +
                               "These are stored in S3 and are not available via the API.",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 20, PropertyName = "credentials")]
    public string? Credentials { get; set; }
}

public class CustomerOriginStrategyClass : Class
{
    public CustomerOriginStrategyClass()
    {
        BootstrapViaReflection(typeof(CustomerOriginStrategy));
    }

    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            "_:customer_originStrategy_", "Origin Strategy", Id,
            "GET", "PUT", "PATCH", "DELETE");


        GetHydraLinkProperty("credentials").SupportedOperations = new Operation[] {new Operation()
        {
            Id = "_:customer_originStrategy_credentials_upsert",
            Method = "PUT",
            Label = "create or replace customer credential objedt",
            Expects = "vocab:Credentials",
            Returns = "vocab:Credentials",
            StatusCodes =new[] {new Status { StatusCode = 201, Description = "Created"} }
        } };
    }
}
