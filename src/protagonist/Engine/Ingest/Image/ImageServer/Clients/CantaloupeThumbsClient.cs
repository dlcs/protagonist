using System.Net;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
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
    private readonly ILogger<CantaloupeThumbsClient> logger;
        
    public CantaloupeThumbsClient(
        HttpClient cantaloupeClient,
        IFileSystem fileSystem,
        IImageManipulator imageManipulator,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor,
        ILogger<CantaloupeThumbsClient> logger)
    {
        this.cantaloupeClient = cantaloupeClient;
        engineSettings = engineOptionsMonitor.CurrentValue;
        this.fileSystem = fileSystem;
        this.imageManipulator = imageManipulator;
        this.logger = logger;
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

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // This is likely an error for the individual thumb size, so just continue
                await LogErrorResponse(response, LogLevel.Information, cancellationToken);
                continue;
            }
            
            if (response.IsSuccessStatusCode)
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
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
                await LogErrorResponse(response, LogLevel.Error, cancellationToken);
                throw new HttpException(response.StatusCode, "failed to retrieve data from the thumbs processor");
            }
        }
        
        return thumbsResponse;
    }

    private async Task LogErrorResponse(HttpResponseMessage response, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.Log(logLevel, "Cantaloupe responded with status code {StatusCode} and body {ErrorResponse}",
            response.StatusCode, errorResponse);
    }
}