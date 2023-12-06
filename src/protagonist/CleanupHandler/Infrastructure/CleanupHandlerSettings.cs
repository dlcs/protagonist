using DLCS.AWS.Settings;
using DLCS.Core.Caching;

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
}