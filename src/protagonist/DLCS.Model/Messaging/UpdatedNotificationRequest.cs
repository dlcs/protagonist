using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class UpdatedNotificationRequest<T> where T : IDeliverable
{
    public T? DeliverableBeforeUpdate { get; set; }
    
    public T? DeliverableAfterUpdate { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}
