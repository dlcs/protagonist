using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;

namespace API.Infrastructure.Messaging;

/// <summary>
/// Represents a change to a single asset - the relevant status before/after change and the change type
/// </summary>
public class AssetModificationRecord
{
    public ChangeType ChangeType { get; }
    public Asset? Before { get; }
    public Asset? After { get; }

    private AssetModificationRecord(ChangeType changeType, Asset? before, Asset? after)
    {
        ChangeType = changeType;
        Before = before;
        After = after;
    }

    public static AssetModificationRecord Delete(Asset before) 
        => new(ChangeType.Delete, before.ThrowIfNull(nameof(before)), null);
    
    public static AssetModificationRecord Update(Asset before, Asset after)
        => new(ChangeType.Update, before.ThrowIfNull(nameof(before)), after.ThrowIfNull(nameof(after)));

    public static AssetModificationRecord Create(Asset after) 
        => new(ChangeType.Create, null, after.ThrowIfNull(nameof(after)));
}