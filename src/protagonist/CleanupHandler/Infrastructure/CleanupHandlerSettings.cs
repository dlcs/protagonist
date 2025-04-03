using DLCS.AWS.Settings;

namespace CleanupHandler.Infrastructure;

public class CleanupHandlerSettings
{
    /// <summary>
    /// Folder template for where image assets are downloaded to
    /// </summary>
    public string? ImageFolderTemplate { get; set; }
    
    /// <summary>
    /// AWS config
    /// </summary>
    public AWSSettings AWS { get; set; }

    /// <summary>
    /// Asset modified settings
    /// </summary>
    public AssetModifiedSettings AssetModifiedSettings { get; set; }
}

public class AssetModifiedSettings
{
    /// <summary>
    /// Whether the removal of data will actually be removed, or only logged
    /// </summary>
    public bool DryRun { get; set; } = false;
    
    /// <summary>
    /// Root URL of the engine
    /// </summary>
    public Uri EngineRoot { get; set; }
}