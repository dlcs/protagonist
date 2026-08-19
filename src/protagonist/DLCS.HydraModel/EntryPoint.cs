using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(EntryPointClass), 
    Description = "The main entry point or homepage of the API.",
    UriTemplate = "")]
public class EntryPoint : DlcsResource
{
    public EntryPoint(string baseUrl)
    {
        BaseUrl = baseUrl;
        Init(baseUrl, true);
    }
    
    [HydraLink(Description = "List of customers to which you have access (usually 1)", 
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "customers")]
    public string? Customers { get; set; }


    [HydraLink(Description = "List of available origin strategies that the DLCS can use to fetch your images.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "originStrategies")]
    public string? OriginStrategies { get; set; }


    [HydraLink(Description = "Available storage policies that can be associated with a Customer or a Space. They determine the " +
                             "number of images and storage capacity permitted to the Customer or Space.",
        Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 18, PropertyName = "storagePolicies")]
    public string? StoragePolicies { get; set; }
}

public class EntryPointClass : Class
{
    public EntryPointClass()
    {
        BootstrapViaReflection(typeof(EntryPoint));
    }

    public override void DefineOperations()
    {
        SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:entry_point",
                Method = "GET",
                Label = "The API's main entry point.",
                Returns = Id
            }
        };

        var customers = GetHydraLinkProperty("customers");
        customers.SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:customer_collection_retrieve",
                Method = "GET",
                Label = "Retrieves all Customer entities",
                Returns = Names.Hydra.Collection
            }
        };


        var originStrategies = GetHydraLinkProperty("originStrategies");
        originStrategies.SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:originStrategy_collection_retrieve",
                Method = "GET",
                Label = "Retrieves all availabe origin strategies. You must use one of these @id URIs as the OriginStrategy property of any CustomerOriginStrategy resources you create.",
                Returns = Names.Hydra.Collection
            }
        };

        var storagePolicies = GetHydraLinkProperty("storagePolicies");
        storagePolicies.SupportedOperations = new[]
        {
            new Operation
            {
                Id = "_:storagePolicy_collection_retrieve",
                Method = "GET",
                Label = "Retrieves available storage polices - maximum image count and storage usage.",
                Returns = Names.Hydra.Collection
            }
        };

    }
}
