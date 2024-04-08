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
    private readonly HttpClient cantaloupeClient;
    private readonly EngineSettings engineSettings;
    private readonly IFileSystem fileSystem;
    private readonly IImageManipulator imageManipulator;
        
    public CantaloupeThumbsClient(
        HttpClient cantaloupeClient,
        IFileSystem fileSystem,
        IImageManipulator imageManipulator,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor)
    {
        this.cantaloupeClient = cantaloupeClient;
        engineSettings = engineOptionsMonitor.CurrentValue;
        this.fileSystem = fileSystem;
        this.imageManipulator = imageManipulator;
    }

    public async Task<List<ImageOnDisk>> GenerateThumbnails(IngestionContext context,
        List<string> thumbSizes, 
        CancellationToken cancellationToken = default)
    {
        var thumbsResponse = new List<ImageOnDisk>();
        
        var convertedS3Location = context.ImageLocation.S3.Replace("/", engineSettings.ImageIngest!.ThumbsProcessorSeparator);

        foreach (var size in thumbSizes)
        {
            using var response =
                await cantaloupeClient.GetAsync(
                    $"iiif/3/{convertedS3Location}/full/{size}/0/default.jpg", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync();
                var assetDirectoryLocation = Path.GetDirectoryName(context.AssetFromOrigin.Location);

                var localThumbsPath =
                    $"{assetDirectoryLocation}{Path.DirectorySeparatorChar}output{Path.DirectorySeparatorChar}thumbs{Path.DirectorySeparatorChar}{size}";
                
                await fileSystem.CreateFileFromStream(localThumbsPath, responseStream, cancellationToken);
                
                using var image = await imageManipulator.LoadAsync(localThumbsPath, cancellationToken);

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
}