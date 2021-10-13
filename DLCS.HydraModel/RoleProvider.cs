using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(RoleProviderClass),
        Description = "Resource that represents the means by which the DLCS acquires roles to enforce "
                    + "an access control session. The DLCS maintains the session, but needs an external auth "
                    + "service (CAS, OAuth etc) to authenticate the user and acquire roles. " +
                    "The RoleProvider contains the configuration information required by the DLCS to " +
                    "interact with a customer's endpoint. "
                    + "The credentials used during the interaction are stored in S3 and not returned via the API.",
        UriTemplate = "/customers/{0}/authServices/{1}/roleProvider")]
    [Unstable(Note = "Under active development")]
    public class RoleProvider : DlcsResource
    {
        [JsonIgnore]
        public string ModelId { get; set; }
        [JsonIgnore]
        public int CustomerId { get; set; }

        public RoleProvider() { }

        public RoleProvider(string baseUrl, int customerId, string authServiceId, string configuration, string credentials)
        {
            CustomerId = customerId;
            ModelId = authServiceId;
            Configuration = configuration;
            Credentials = credentials;
            Init(baseUrl, true, customerId, ModelId);
        }

        [RdfProperty(Description = "JSON configuration blob for this particular service",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "configuration")]
        public string Configuration { get; set; }

        [RdfProperty(Description = "Credentials - not exposed via API, but can be written to by customer.",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = true)]
        [JsonProperty(Order = 12, PropertyName = "credentials")]
        public string Credentials { get; set; }
    }

    public class RoleProviderClass : Class
    {
        public RoleProviderClass()
        {
            BootstrapViaReflection(typeof(RoleProvider));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:customer_authService_roleProvider_", "Role Provider", Id,
                "GET", "PUT", "PATCH", "DELETE");
        }
    }
}
