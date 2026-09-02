using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Repository.OriginStrategies;

namespace Engine.Ingest;

/// <summary>
/// Base class to help with ingest operations. Contains methods common across different <see cref="IDeliverable"/>
/// types 
/// </summary>
public abstract class DeliverableIngester(ICustomerOriginStrategyRepository customerOriginRepository)
{
    protected async Task<CustomerOriginStrategy?> GetCustomerOriginStrategy(IDeliverable deliverable)
    {
        try
        {
            var customerOriginStrategy = await customerOriginRepository.GetCustomerOriginStrategy(deliverable);
            return customerOriginStrategy;
        }
        catch (OriginStrategyRegexException originStrategyRegexException)
        {
            deliverable.Error = originStrategyRegexException.Message;
        }

        return null;
    }
}
