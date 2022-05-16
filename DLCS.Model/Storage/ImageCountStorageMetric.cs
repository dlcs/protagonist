namespace DLCS.Model.Storage
{
    public record ImageCountStorageMetric
    {
        public long MaximumNumberOfStoredImages { get; set; }
        public long CurrentNumberOfStoredImages { get; set; }
    }
}