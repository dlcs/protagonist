namespace Engine.Settings;

public class CustomerOverridesSettings
{
    public static CustomerOverridesSettings Empty = new() {CustomerName = "_default_"};
        
    /// <summary>
    /// Friendly name of customer (keyed by Id in appsettings).
    /// </summary>
    public string CustomerName { get; set; }
        
    /// <summary>
    /// Whether image should immediately be orchestrated after ingestion.
    /// </summary>
    public bool? OrchestrateImageAfterIngest { get; set; }
        
    /// <summary>
    /// If true, StoragePolicy is not checked on ingestion.
    /// </summary>
    public bool NoStoragePolicyCheck { get; set; }
        
    /// <summary>
    /// Denotes whether we have full access to origin for 'T' assets.
    /// </summary>
    /// <remarks>
    /// This assumes that ALL S3-hosted T assets for customer will come from a bucket where we have S3 access.
    /// </remarks>
    public bool FullBucketAccess { get; set; }
}