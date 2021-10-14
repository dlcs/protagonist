using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(OriginStrategyClass),
        Description = "As a customer you can provide information to the DLCS to allow it to fetch your images " +
                      "from their origin endpoints. Every customer is given a default origin strategy, which is for the " +
                      "DLCS to attempt to fetch the image from its origin URL without presenting credentials. " +
                      "This is fine for images that are publicly available, but is unlikely to be appropriate for " +
                      "images you are exposing from your asset management system. You might have a service that is " +
                      "available only to the DLCS, or an FTP site.  ",
        UriTemplate = "/originStrategies/{0}")]
    [Unstable(Note = "Under active development")]
    public class OriginStrategy : DlcsResource
    {
        [JsonIgnore]
        public string ModelId { get; set; }

        public OriginStrategy(string baseUrl, string originStrategyId)
        {
            ModelId = originStrategyId;
            Init(baseUrl, true, originStrategyId);
        }
         
        // TODO HERE MOVE TO MOCK HELP
        public OriginStrategy(string baseUrl, string originStrategyId, string name, bool requiresCredentials)
        {
            ModelId = originStrategyId;
            Init(baseUrl, true, originStrategyId);
            Name = name;
            RequiresCredentials = requiresCredentials;
        }

        [RdfProperty(Description = "The human readable name of the origin strategy",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "name")]
        public string Name { get; set; }



        [RdfProperty(Description = "Whether the DLCS needs stored credentials to fetch images with this strategy",
            Range = Names.XmlSchema.Boolean, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "requiresCredentials")]
        public bool RequiresCredentials { get; set; }
    }


    public class OriginStrategyClass : Class
    {
        public OriginStrategyClass()
        {
            BootstrapViaReflection(typeof(OriginStrategy));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:originStrategy_", "Origin Strategy", Id,
                "GET");
        }
    }
}
