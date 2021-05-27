#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class StoragePolicy
    {
        public string Id { get; set; }
        public long MaximumNumberOfStoredImages { get; set; }
        public long MaximumTotalSizeOfStoredImages { get; set; }
    }
}
