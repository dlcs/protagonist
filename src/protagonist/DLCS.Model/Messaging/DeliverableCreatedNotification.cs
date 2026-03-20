using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class DeliverableCreatedNotification<T> where T : IDeliverable
{
    public T? Deliverable { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
}
