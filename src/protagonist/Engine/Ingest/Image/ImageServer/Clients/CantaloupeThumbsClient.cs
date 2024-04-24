using System.Net;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using Engine.Ingest.Image.ImageServer.Measuring;
using IIIF;
using IIIF.ImageApi;

namespace Engine.Ingest.Image.ImageServer.Clients;

/// <summary>
/// Implementation of <see cref="IThumbCreator"/> using Cantaloupe for generation
/// </summary>
public class CantaloupeThumbsClient : IThumbsClient
{
    private readonly HttpClient cantaloupeClient;
    private readonly IFileSystem fileSystem;
    private readonly IImageMeasurer imageMeasurer;
    private readonly ILogger<CantaloupeThumbsClient> logger;
        
    public CantaloupeThumbsClient(
        HttpClient cantaloupeClient,
        IFileSystem fileSystem,
        IImageMeasurer imageMeasurer,
        ILogger<CantaloupeThumbsClient> logger)
    {
        this.cantaloupeClient = cantaloupeClient;
        this.fileSystem = fileSystem;
        this.imageMeasurer = imageMeasurer;
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
                ValidateSize(size, imageSize, imageOnDisk, assetId);
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
        logger.LogTrace("Saving thumb for {ThumbSize} to {ThumbLocation}", size, localThumbsPath);
                
        await fileSystem.CreateFileFromStream(localThumbsPath, responseStream, cancellationToken);
                
        var imageOnDisk = await imageMeasurer.MeasureImage(localThumbsPath, cancellationToken);
        return imageOnDisk;
    }

    private async Task LogErrorResponse(HttpResponseMessage response, AssetId assetId, string size, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.Log(logLevel,
            "Cantaloupe responded with status code {StatusCode} when processing Asset {AssetId}, size '{Size}' and body {ErrorResponse}",
            response.StatusCode, assetId, size, errorResponse);
    }

    private void ValidateSize(string sizeParam, Size originSize, ImageOnDisk imageOnDisk, AssetId assetId)
    {
        var actualSize = new Size(imageOnDisk.Width, imageOnDisk.Height);
        var sizeParameter = SizeParameter.Parse(sizeParam);
        var expectedSize = sizeParameter.GetResultingSize(originSize);
        
        if (expectedSize.ToString() == actualSize.ToString()) return;

        if (sizeParameter.Confined)
        {
            // always need longest to match. e.g. for !400,400: 299,400 + 301,400 are ok. 300,401 + 300,399 are not
            HandleMismatch(expectedSize.MaxDimension == actualSize.MaxDimension);
            return;
        }

        if (sizeParameter.Width.HasValue)
        {
            // always need w to match. e.g. for 400,: 400,500 + 400,499 are ok. 399,500 + 401,500 are not
            HandleMismatch(expectedSize.Width == actualSize.Width);
            return;
        }

        if (sizeParameter.Height.HasValue)
        {
            // always need h to match. e.g. for ,500: 399,500 + 401,500 are ok. 400,499 + 400,501 are not
            HandleMismatch(expectedSize.Height == actualSize.Height);
            return;
        }

        void HandleMismatch(bool allowed)
        {
            if (allowed)
            {
                logger.LogTrace(
                    "Size mismatch for {AssetId}, size '{Size}'. Expected:'{Expected}', actual:'{Actual}'.",
                    assetId, sizeParam, expectedSize, actualSize);
                return;
            }
            
            logger.LogWarning(
                "Size mismatch for {AssetId}, size '{Size}'. Expected:'{Expected}', actual:'{Actual}'. Using expected size",
                assetId, sizeParam, expectedSize, actualSize);
            imageOnDisk.Width = expectedSize.Width;
            imageOnDisk.Height = expectedSize.Height;
        }
    }
}