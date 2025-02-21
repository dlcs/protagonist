using System;
using System.Text;
using DLCS.Core.Collections;
using DLCS.Core.Encryption;
using DLCS.Model.Customers;

namespace DLCS.Web.Auth;

/// <summary>
/// Class with helpers to get basic authentication credentials for DLCS API.
/// </summary>
public class DlcsApiAuth
{
    private readonly IEncryption encryption;

    public DlcsApiAuth(IEncryption encryption)
    {
        this.encryption = encryption;
    }

    /// <summary>
    /// Get base-64 encoded string contains basic authentication details for customer.
    /// </summary>
    /// <param name="customer"><see cref="Customer"/> to get credentials for.</param>
    /// <param name="salt">ApiSalt for generating API key.</param>
    /// <returns>base-64 basic authentication string if found, else null</returns>
    public string? GetBasicAuthForCustomer(Customer customer, string salt)
    {
        if (customer.Keys.IsNullOrEmpty()) return null;
        
        string apiKey = customer.Keys[0];
        var apiSecret = GetApiSecret(customer, salt, apiKey);
        return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:{apiSecret}"));
    }

    /// <summary>
    /// Get the API secret for customer, salt and api-key
    /// </summary>
    /// <param name="customer"><see cref="Customer"/> to get credentials for.</param>
    /// <param name="salt">ApiSalt for generating API key.</param>
    /// <param name="apiKey">Customers API key</param>
    /// <returns></returns>
    public string GetApiSecret(Customer customer, string salt, string apiKey)
    {
        return encryption.Encrypt(string.Concat(salt, customer.Id, apiKey));
    }
}