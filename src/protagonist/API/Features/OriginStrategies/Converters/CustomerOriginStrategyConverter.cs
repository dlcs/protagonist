using DLCS.Core.Enum;
using DLCS.Core.Strings;

namespace API.Features.OriginStrategies.Converters;

public static class CustomerOriginStrategyConverter
{
    public static DLCS.HydraModel.CustomerOriginStrategy ToHydra(this DLCS.Model.Customers.CustomerOriginStrategy originStrategy, string baseUrl)
    {
        var hydraOriginStrategy = new DLCS.HydraModel.CustomerOriginStrategy(baseUrl, originStrategy.Customer, originStrategy.Id)
        {
            Regex = originStrategy.Regex,
            OriginStrategy = $"{baseUrl}/originStrategies/{originStrategy.Strategy.GetDescription()}", 
            // Credentials should be hidden when returned to the user
            Credentials = "xxx",
            Optimised = originStrategy.Optimised,
            Order = originStrategy.Order,
        };
        
        return hydraOriginStrategy;
    }
    
    public static DLCS.Model.Customers.CustomerOriginStrategy ToDlcsModel(this DLCS.HydraModel.CustomerOriginStrategy hydraOriginStrategy)
    {
        var originStrategy = new DLCS.Model.Customers.CustomerOriginStrategy()
        {
            Id = hydraOriginStrategy.ModelId,
            Customer = hydraOriginStrategy.CustomerId,
            Regex = hydraOriginStrategy.Regex,
            Credentials = hydraOriginStrategy.Credentials,
        };
        
        if (hydraOriginStrategy.OriginStrategy.HasText())
            originStrategy.Strategy = hydraOriginStrategy.OriginStrategy
                .GetEnumFromString<DLCS.Model.Customers.OriginStrategyType>();
        
        if (hydraOriginStrategy.Optimised.HasValue) 
            originStrategy.Optimised = hydraOriginStrategy.Optimised.Value;

        if (hydraOriginStrategy.Order.HasValue) 
            originStrategy.Order = hydraOriginStrategy.Order.Value;
        
        return originStrategy;
    }
}