using DLCS.HydraModel;

namespace API.Converters;

public static class PortalUserConverter
{
    public static PortalUser ToHydra(this DLCS.Model.Customers.User dbUser, string baseUrl)
    {
        var portalUser = new PortalUser(baseUrl, dbUser.Customer, dbUser.Id)
        {
            Created = dbUser.Created,
            Email = dbUser.Email,
            Enabled = dbUser.Enabled
        };
        return portalUser;
    }
}