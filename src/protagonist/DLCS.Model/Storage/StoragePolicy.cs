#nullable disable

using System.Diagnostics;

namespace DLCS.Model.Storage;

[DebuggerDisplay("{Id}")]
public class StoragePolicy
{
    public string Id { get; set; }
    public long MaximumNumberOfStoredImages { get; set; }
    public long MaximumTotalSizeOfStoredImages { get; set; }
    
    public const string DefaultStoragePolicyName = "default";
}
