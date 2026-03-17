using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class UpdatedNotificationRequest<T> where T : IDeliverable //todo: name changes need to be called out on PR
{
    public T? DeliverableBeforeUpdate { get; set; }
    
    public T? DeliverableAfterUpdate { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}
