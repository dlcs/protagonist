using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class AssetUpdatedNotificationRequest
{
    public Asset? AssetBeforeUpdate { get; set; }
    
    public Asset? AssetAfterUpdate { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}