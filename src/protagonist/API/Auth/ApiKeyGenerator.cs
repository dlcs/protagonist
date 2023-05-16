using API.Settings;
using DLCS.Model.Customers;
using DLCS.Web.Auth;
using Microsoft.Extensions.Options;

namespace API.Auth;

public class ApiKeyGenerator
{
    private readonly ApiSettings settings;
    private readonly DlcsApiAuth encryption;

    public ApiKeyGenerator(DlcsApiAuth encryption, IOptions<ApiSettings> options)
    {
        this.encryption = encryption;
        settings = options.Value;
    }
    
    /// <summary>
    /// Generate a new api key and secret for specified customer
    /// </summary>
    /// <param name="customer">Customer to create api key for</param>
    /// <returns>ApiKey and Secret</returns>
    public (string key, string secret) CreateApiKey(Customer customer)
    {
        var apiKey = Guid.NewGuid().ToString();
        var apiSecret = encryption.GetApiSecret(customer, settings.Salt, apiKey);

        return (apiKey, apiSecret);
    }
}