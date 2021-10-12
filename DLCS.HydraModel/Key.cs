using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(KeyClass),
        Description = "Credentials for accessing the API. The Key object will only have the accompanying secret field returned" +
                      " once, when a new key is created. Thereafter only the key is available from the API.",
        UriTemplate = "/customers/{0}/keys/{1}")]
    public class Key : DlcsResource
    {
        [JsonIgnore]
        public string ModelId { get; set; }

        [JsonIgnore]
        public int CustomerId { get; set; }

        public Key()
        {
            
        }

        public Key(string modelId, int customerId, string key, string secret)
        {
            ModelId = modelId;
            CustomerId = customerId;

        }

        [RdfProperty(Description = "API Key",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "key")]
        public string KeyString { get; set; }


        [RdfProperty(Description = "API Secret (available at creation time only)",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "secret")]
        public string Secret { get; set; }
    }

    public class KeyClass : Class
    {
        public KeyClass()
        {
            BootstrapViaReflection(typeof(Key));
        }
        public override void DefineOperations()
        {
            SupportedOperations = GetSpecialKeyOperations();
        }

        public static Operation[] GetSpecialKeyOperations()
        {
            return new[]
            {
                new Operation
                {
                    Id = "_:customer_keys_retrieve",
                    Method = "GET",
                    Label = "Returns keys allocated to this customer resource",
                    Returns = "vocab:Key"
                },
                new Operation
                {
                    Id = "_:customer_keys_create_key",
                    Method = "POST",
                    Label = "Submit an empty POST and the DLCS will generate a key and secret. Requires eleveated ",
                    Description = "The secret is only available once in the returned key.",
                    Expects = Names.Owl.Nothing, // 
                    Returns = "vocab:Key",
                    StatusCodes = new[]
                    {
                        new Status
                        {
                            StatusCode = 201,
                            Description = "Job has been accepted - key created and returned"
                        }
                    }
                }
            };
        }

    }
}
