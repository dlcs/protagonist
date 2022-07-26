namespace DLCS.Model.Storage;

public record ImageCountStorageMetric
{
    public string PolicyId { get; set; }
    public long MaximumNumberOfStoredImages { get; set; }
    public long CurrentNumberOfStoredImages { get; set; }
    public long MaximumTotalSizeOfStoredImages { get; set; }
    public long CurrentTotalSizeStoredImages { get; set; }
}