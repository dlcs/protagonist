namespace DLCS.Model.Storage;

/// <summary>
/// While CustomerStorage is by Space, we need an overview to make decisions
/// </summary>
public class CustomerStorageSummary
{
    public int CustomerId { get; set; }
    public long NumberOfStoredImages { get; set; }
    public long TotalSizeOfStoredImages { get; set; }
    public long TotalSizeOfThumbnails { get; set; }
}