#nullable disable

using System.Diagnostics;

namespace DLCS.Model.Storage
{
    [DebuggerDisplay("{Id}")]
    public partial class StoragePolicy
    {
        public string Id { get; set; }
        public long MaximumNumberOfStoredImages { get; set; }
        public long MaximumTotalSizeOfStoredImages { get; set; }
    }
}
