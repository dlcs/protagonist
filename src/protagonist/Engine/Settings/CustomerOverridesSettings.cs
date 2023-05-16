namespace Engine.Settings;

public class CustomerOverridesSettings
{
    public static readonly CustomerOverridesSettings Empty = new();

    /// <summary>
    /// Whether image should immediately be orchestrated after ingestion.
    /// </summary>
    public bool? OrchestrateImageAfterIngest { get; set; }
        
    /// <summary>
    /// If true, StoragePolicy is not checked on ingestion.
    /// </summary>
    public bool NoStoragePolicyCheck { get; set; }
}