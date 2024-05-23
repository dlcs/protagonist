using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Templates;
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
    public static string GetSourceFolder(Asset asset, string ingestId, EngineSettings engineSettings)
    {
        var imageIngest = engineSettings.ImageIngest;
        var workingFolder = GetWorkingFolder(ingestId, imageIngest);
        
        // source is the main folder for storing downloaded image
        var assetId = new AssetId(asset.Id.Customer, asset.Id.Space,
            asset.Id.Asset.Replace("(", imageIngest.OpenBracketReplacement)
                .Replace(")", imageIngest.CloseBracketReplacement));
        var source = TemplatedFolders.GenerateFolderTemplate(imageIngest.SourceTemplate, assetId, root: workingFolder);
        return source;
    }
}