using System.Collections.Generic;
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
    
    public List<string>? DeleteFrom { get; }
 
    private AssetModificationRecord(ChangeType changeType, Asset? before, Asset? after, List<string>? deleteFrom)
    {
        ChangeType = changeType;
        Before = before;
        After = after;
        DeleteFrom = deleteFrom;
    }

    public static AssetModificationRecord Delete(Asset before, List<string> deleteFrom) 
        => new(ChangeType.Delete, before.ThrowIfNull(nameof(before)), null, deleteFrom.ThrowIfNull(nameof(deleteFrom)));
    
    public static AssetModificationRecord Update(Asset before, Asset after)
        => new(ChangeType.Update, before.ThrowIfNull(nameof(before)), after.ThrowIfNull(nameof(after)), null);

    public static AssetModificationRecord Create(Asset after) 
        => new(ChangeType.Create, null, after.ThrowIfNull(nameof(after)), null);
}