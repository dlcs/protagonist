using DLCS.AWS.Configuration;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Repository.OriginStrategies;

namespace Engine.Ingest;

/// <summary>
/// Base class to help with ingest operations. Contains methods common across different <see cref="IDeliverable"/>
/// types
/// </summary>
public abstract class DeliverableIngester(
    ICustomerOriginStrategyRepository customerOriginRepository,
    ICustomerAwsContext customerAwsContext)
{
    /// <summary>
    /// Record which customer this ingest is for, AWS clients are scoped to that customer. This must be set before
    /// any AWS request is made.
    /// </summary>
    /// <returns>Object that clears the customer when disposed</returns>
    protected IDisposable SetCustomerAwsContext(AssetId assetId) => customerAwsContext.SetCustomer(assetId.Customer);

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
