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
    private readonly IFileSystem fileSystem;
    private readonly IImageManipulator imageManipulator;
    private readonly ILogger<CantaloupeThumbsClient> logger;
        
    public CantaloupeThumbsClient(
        HttpClient cantaloupeClient,
        IFileSystem fileSystem,
        IImageManipulator imageManipulator,
        ILogger<CantaloupeThumbsClient> logger)
    {
        this.cantaloupeClient = cantaloupeClient;
        this.fileSystem = fileSystem;
        this.imageManipulator = imageManipulator;
        this.logger = logger;
    }

    public async Task<List<ImageOnDisk>> GenerateThumbnails(IngestionContext context,
        List<string> thumbSizes,
        string thumbFolder,
        CancellationToken cancellationToken = default)
    {
        var thumbsResponse = new List<ImageOnDisk>();

        const string pathReplacement = "%2f";
        var convertedS3Location = context.ImageLocation.S3.Replace("/", pathReplacement);

        var count = 0;
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

                var localThumbsPath = Path.Join(thumbFolder, $"thumb{++count}");
                logger.LogDebug("Saving thumb for {ThumbSize} to {ThumbLocation}", size, localThumbsPath);
                
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