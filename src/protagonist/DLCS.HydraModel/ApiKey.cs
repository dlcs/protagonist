using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(ApiKeyClass),
    Description = "Credentials for accessing the API. The Key object will only have the accompanying secret field returned" +
                  " once, when a new key is created. Thereafter only the key is available from the API.",
    UriTemplate = "/customers/{0}/keys/{1}")]
public class ApiKey : DlcsResource
{
    [JsonIgnore]
    public int CustomerId { get; set; }
    
    public ApiKey()
    {
         
    }
    
    public ApiKey(string baseUrl,  int customerId, string key, string? secret)
    {
        Init(baseUrl, false, customerId, key);
        Key = key;
        Secret = secret;
        CustomerId = customerId; 
    }

    private string? key;

    [RdfProperty(Description = "API Key",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "key")]
    public string? Key
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            if (!string.IsNullOrWhiteSpace(Id))
            {
                return GetLastPathElement();
            }
            return null;
        }
        set => key = value;
    }


    [RdfProperty(Description = "API Secret (available at creation time only)",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "secret")]
    public string? Secret { get; set; }
    
    public override string Type => "vocab:Key";
}

public class ApiKeyClass : Class
{
    public ApiKeyClass()
    {
        BootstrapViaReflection(typeof(ApiKey));
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
