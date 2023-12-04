using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class AssetModifiedNotificationRequest
{
    public Asset? Asset { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}