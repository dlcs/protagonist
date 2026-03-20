using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class DeliverableDeletedNotification<T> where T : IDeliverable
{
    public T? Deliverable { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
    
    public ImageCacheType DeleteFrom { get; set; }
}
