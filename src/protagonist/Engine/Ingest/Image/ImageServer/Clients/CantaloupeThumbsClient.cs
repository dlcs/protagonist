using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Templates;
using Engine.Ingest.Image.ImageServer.Manipulation;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer.Clients;

public class CantaloupeThumbsClient : ICantaloupeThumbsClient
{
    private readonly HttpClient thumbsClient;
    private readonly EngineSettings engineSettings;
    private readonly IFileSystem fileSystem;
    private readonly IImageManipulator imageManipulator;
        
    public CantaloupeThumbsClient(
        HttpClient thumbsClient,
        IFileSystem fileSystem,
        IImageManipulator imageManipulator,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor)
    {
        this.thumbsClient = thumbsClient;
        engineSettings = engineOptionsMonitor.CurrentValue;
        this.fileSystem = fileSystem;
        this.imageManipulator = imageManipulator;
    }

    public async Task<List<ImageOnDisk>> CallCantaloupe(IngestionContext context, 
        AssetId modifiedAssetId,
        List<string> thumbSizes, 
        CancellationToken cancellationToken = default)
    {
        var thumbsResponse = new List<ImageOnDisk>();
        
        var convertedS3Location = context.ImageLocation.S3.Replace("/", engineSettings.ImageIngest!.ThumbsProcessorSeparator);

        foreach (var size in thumbSizes!)
        {
            using var response =
                await thumbsClient.GetAsync(
                    $"iiif/3/{convertedS3Location}/full/{size}/0/default.jpg", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync();
                var assetDirectoryLocation = Path.GetDirectoryName(context.AssetFromOrigin.Location);

                var localThumbsPath =
                    $"{assetDirectoryLocation}{Path.DirectorySeparatorChar}output{Path.DirectorySeparatorChar}thumbs{Path.DirectorySeparatorChar}{size}";
                
                await fileSystem.CreateFileFromStream(localThumbsPath, responseStream, cancellationToken);
                
                var image = await imageManipulator.LoadAsync(localThumbsPath, cancellationToken);

                thumbsResponse.Add(new ImageOnDisk()
                {
                    Path = localThumbsPath,
                    Width = image.Width,
                    Height = image.Height
                });
            }
            else
            {
                throw new HttpException(response.StatusCode, "failed to retrieve data from the thumbs processor");
            }
        }
        
        return thumbsResponse;
    }
    
    private string GetRelativeLocationOnDisk(IngestionContext context, AssetId modifiedAssetId)
    {
        var assetOnDisk = context.AssetFromOrigin.Location;
        var extension = assetOnDisk.EverythingAfterLast('.');

        // this is to get it working nice locally as appetiser/tizer root needs to be unix + relative to it
        var imageProcessorRoot = engineSettings.ImageIngest.GetRoot(true);
        var unixPath = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.SourceTemplate, modifiedAssetId,
            root: imageProcessorRoot);

        unixPath += $"/{modifiedAssetId.Asset}.{extension}";
        return unixPath;
    }
}