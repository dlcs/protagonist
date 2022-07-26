#nullable disable

using System;

namespace DLCS.Model.Storage
{
    public partial class CustomerStorage
    {
        public int Customer { get; set; }
        public string StoragePolicy { get; set; }
        public long NumberOfStoredImages { get; set; }
        public long TotalSizeOfStoredImages { get; set; }
        public long TotalSizeOfThumbnails { get; set; }
        public DateTime? LastCalculated { get; set; }
        public int Space { get; set; }
    }
}
