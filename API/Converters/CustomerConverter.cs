using DLCS.HydraModel;
using Newtonsoft.Json.Linq;

namespace API.Converters
{
    public static class CustomerConverter
    {
        public static Customer ToHydra(this DLCS.Model.Customers.Customer dbCustomer, string baseUrl)
        {
            return new(baseUrl, dbCustomer.Id, dbCustomer.Name, dbCustomer.DisplayName);
        }
        
        
        public static JObject ToCollectionForm(this DLCS.Model.Customers.Customer dbCustomer, string baseUrl)
        {
            var customer = new Customer(baseUrl, dbCustomer.Id, false);
            return customer.GetCollectionForm();
        }
    }
}