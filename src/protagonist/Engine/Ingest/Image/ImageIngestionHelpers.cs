using DLCS.Core.Types;
using DLCS.Model.Templates;
using Engine.Ingest.Persistence;
using Engine.Settings;

namespace Engine.Ingest.Image;

internal static class ImageIngestionHelpers
{
    /// <summary>
    /// Get the top level working directory for an ingest request
    /// </summary>
    public static string GetWorkingFolder(string ingestId, ImageIngestSettings imageIngestSettings, bool forImageProcessor = false)
    {
        return $"{Path.Combine(imageIngestSettings.GetRoot(forImageProcessor), ingestId)}{Path.DirectorySeparatorChar}";
    }
    
    /// <summary>
    /// Get folder location where working assets are to be saved to
    /// </summary>
    public static string GetSourceFolder(IngestionContext ingestionContext, EngineSettings engineSettings)
    {
        var imageIngest = engineSettings.ImageIngest;
        var workingFolder = GetWorkingFolder(ingestionContext.IngestId, imageIngest);
        
        // source is the main folder for storing downloaded image
        var assetId = new AssetId(ingestionContext.AssetId.Customer, ingestionContext.AssetId.Space,
            ingestionContext.AssetId.GetDiskSafeAssetId(imageIngest));
        var source = TemplatedFolders.GenerateFolderTemplate(imageIngest.SourceTemplate, assetId, root: workingFolder);
        return source;
    }
}
