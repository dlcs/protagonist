using System.Linq;
using DLCS.Model.Assets;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Assets;

/// <summary>
/// Extension methods for asset queries.
/// </summary>
public static class AssetQueryX
{
    /// <summary>
    /// Include asset delivery channels and their associated policies.
    /// </summary>
    public static IQueryable<Asset> IncludeDeliveryChannelsWithPolicy(this IQueryable<Asset> assetQuery)
        => assetQuery
            .Include(a => a.ImageDeliveryChannels.OrderBy(idc => idc.Channel))
            .ThenInclude(dc => dc.DeliveryChannelPolicy);
}
