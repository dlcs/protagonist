using DLCS.Repository;
using Test.Helpers.Integration;

namespace API.Tests;

/// <summary>
/// Helper methods and constants for working with LegacyMode. LegacyCustomer + Space are configured as such via
/// appsettings.Testing.json
/// </summary>
public class LegacyModeHelpers
{
    public const int LegacyCustomer = 325665;
    public const int LegacySpace = 201;
    public const int NonLegacySpace = 4;
    
    public static async Task SetupLegacyCustomer(DlcsContext dbContext, int space = LegacySpace)
    {
        await dbContext.Customers.AddTestCustomer(LegacyCustomer);
        await dbContext.Spaces.AddTestSpace(LegacyCustomer, space);
        await dbContext.Spaces.AddTestSpace(LegacyCustomer, NonLegacySpace);
        await dbContext.CustomerStorages.AddTestCustomerStorage(LegacyCustomer);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(LegacyCustomer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(LegacyCustomer);
        await dbContext.SaveChangesAsync();
    }
}