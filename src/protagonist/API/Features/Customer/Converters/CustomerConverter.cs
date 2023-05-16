using Newtonsoft.Json.Linq;

namespace API.Features.Customer.Converters;

/// <summary>
/// Conversion between API and EF model forms of resources.
/// </summary>
public static class CustomerConverter
{
    /// <summary>
    /// Converts the EF model object to an API resource.
    /// </summary>
    public static DLCS.HydraModel.Customer ToHydra(this DLCS.Model.Customers.Customer dbCustomer, string baseUrl)
    {
        var customer = new DLCS.HydraModel.Customer(baseUrl, dbCustomer.Id, dbCustomer.Name, dbCustomer.DisplayName)
        {
            Created = dbCustomer.Created,
            Administrator = dbCustomer.Administrator,
            AcceptedAgreement = dbCustomer.AcceptedAgreement
        };
        return customer;
    }
    
    /// <summary>
    /// Converts the EF model object to a simplified JObject with just @id and @type
    /// </summary>
    public static JObject ToCollectionForm(this DLCS.Model.Customers.Customer dbCustomer, string baseUrl)
    {
        var customer = new DLCS.HydraModel.Customer(baseUrl, dbCustomer.Id, false);
        return customer.GetCollectionForm();
    }
}