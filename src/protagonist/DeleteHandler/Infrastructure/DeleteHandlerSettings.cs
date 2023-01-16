using DLCS.AWS.Settings;

namespace DeleteHandler.Infrastructure;

public class DeleteHandlerSettings
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