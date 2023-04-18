using DLCS.Model.Assets;
using DLCS.Model.Templates;
using Engine.Settings;

namespace Engine.Ingest.Image;

internal static class ImageIngestionHelpers
{
    /// <summary>
    /// Get folder location where working assets are to be saved to
    /// </summary>
    public static string GetSourceFolder(Asset asset, EngineSettings engineSettings)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();
            
        // source is the main folder for storing downloaded image
        var assetId = asset.Id;
        var source = TemplatedFolders.GenerateFolderTemplate(imageIngest.SourceTemplate, assetId, root: root);
        return source;
    }
}