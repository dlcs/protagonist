using DLCS.AWS.Settings;
using DLCS.Core.Settings;

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

    public AssetModifiedSettings AssetModifiedSettings { get; set; }
}

public class AssetModifiedSettings
{
    /// <summary>
    /// Whether the removal of data will actually be removed, or only logged
    /// </summary>
    public bool DryRun { get; set; } = false;
    
    /// <summary>
    /// Which image-server is handling downstream tile requests
    /// </summary>
    /// <remarks>
    /// Ideally the Orchestrator should be agnostic to this but, for now at least, the downstream image server will
    /// be useful to know for toggling some functionality
    /// </remarks>
    public ImageServer ImageServer { get; set; } = ImageServer.Cantaloupe;
    
    /// <summary>
    /// Root URL of the engine
    /// </summary>
    public Uri EngineRoot { get; set; }
}