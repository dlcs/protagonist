using System.Net;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using Engine.Ingest.Image.ImageServer.Manipulation;
using IIIF;
using IIIF.ImageApi;

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
        var imageSize = new Size(context.Asset.Width ?? 0, context.Asset.Height ?? 0);
        var assetId = context.AssetId;

        const string pathReplacement = "%2f";
        var convertedS3Location = context.ImageLocation.S3.Replace("/", pathReplacement);

        var count = 0;
        foreach (var size in thumbSizes)
        {
            ++count;
            using var response =
                await cantaloupeClient.GetAsync(
                    $"iiif/3/{convertedS3Location}/full/{size}/0/default.jpg", cancellationToken);
            
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // This is likely an error for the individual thumb size, so just continue
                await LogErrorResponse(response, assetId, size, LogLevel.Information, cancellationToken);
                continue;
            }
            
            if (response.IsSuccessStatusCode)
            {
                var imageOnDisk = await SaveImageToDisk(response, size, thumbFolder, count, cancellationToken);
                thumbsResponse.Add(imageOnDisk);
                ValidateReturnedSize(size, Math.Max(imageOnDisk.Width, imageOnDisk.Height), imageSize, assetId);
            }
            else
            {
                await LogErrorResponse(response, assetId, size, LogLevel.Error, cancellationToken);
                throw new HttpException(response.StatusCode, "failed to retrieve data from the thumbs processor");
            }
        }
        
        return thumbsResponse;
    }

    private async Task<ImageOnDisk> SaveImageToDisk(HttpResponseMessage response, string size, string thumbFolder,
        int count, CancellationToken cancellationToken)
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var localThumbsPath = Path.Join(thumbFolder, $"thumb{count}");
        logger.LogDebug("Saving thumb for {ThumbSize} to {ThumbLocation}", size, localThumbsPath);
                
        await fileSystem.CreateFileFromStream(localThumbsPath, responseStream, cancellationToken);
                
        using var image = await imageManipulator.LoadAsync(localThumbsPath, cancellationToken);

        var imageOnDisk = new ImageOnDisk
        {
            Path = localThumbsPath,
            Width = image.Width,
            Height = image.Height
        };
        return imageOnDisk;
    }

    private async Task LogErrorResponse(HttpResponseMessage response, AssetId assetId, string size, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.Log(logLevel,
            "Cantaloupe responded with status code {StatusCode} when processing Asset {AssetId}, size '{Size}' and body {ErrorResponse}",
            response.StatusCode, assetId, size, errorResponse);
    }
    
    private void ValidateReturnedSize(string sizeParam, int actualMaxDimension, Size originSize, AssetId assetId)
    {
        var sizeParameter = SizeParameter.Parse(sizeParam);
        var expectedSize = sizeParameter.GetResultingSize(originSize);
        var expectedMax = expectedSize.MaxDimension;
        if (expectedMax != actualMaxDimension)
        {
            logger.LogWarning(
                "Possible size mismatch for asset {AssetId}, size {Size}. Expected maxDimension to be {ExpectedMax} but got {ActualMax}",
                assetId, sizeParam, expectedMax, actualMaxDimension);
        }
    }
}