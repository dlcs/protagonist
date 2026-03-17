using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;

namespace API.Infrastructure.Messaging.General;

public class NotificationRecord<T> where T : class, IDeliverable
{
    public ChangeType ChangeType { get; }
    
    public T? Before { get; }
    public T? After { get; }
    
    public bool EngineNotified { get; }
    
    public ImageCacheType? DeleteFrom { get; }
    
    private NotificationRecord(ChangeType changeType, T? before, T? after, ImageCacheType? deleteFrom, bool assetModifiedEngineNotified)
    {
        ChangeType = changeType;
        Before = before;
        After = after;
        DeleteFrom = deleteFrom;
        EngineNotified = assetModifiedEngineNotified;
    }
    
    public static NotificationRecord<T> Delete(T before, ImageCacheType deleteFrom)
        => new(ChangeType.Delete, before.ThrowIfNull(nameof(before)), null, deleteFrom.ThrowIfNull(nameof(deleteFrom)),
            false);

    public static NotificationRecord<T> Update(T before, T after, bool assetModifiedEngineNotified)
        => new(ChangeType.Update, before.ThrowIfNull(nameof(before)), after.ThrowIfNull(nameof(after)), null,
            assetModifiedEngineNotified);

    public static NotificationRecord<T> Create(T after)
        => new(ChangeType.Create, null, after.ThrowIfNull(nameof(after)), null, false);
}
