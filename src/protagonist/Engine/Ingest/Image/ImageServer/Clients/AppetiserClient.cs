using DLCS.Core.Guard;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Templates;
using DLCS.Web.Requests;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using IIIF.ImageApi;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer.Clients;

/// <summary>
/// Implementation of <see cref="IImageProcessorClient"/>, using Appetiser for processing
/// </summary>
public class AppetiserClient(
    HttpClient appetiserClient,
    IOptionsMonitor<EngineSettings> engineOptionsMonitor,
    ILogger<AppetiserClient> logger)
    : IImageProcessorClient
{
    private readonly ImageIngestSettings imageIngest = engineOptionsMonitor.CurrentValue.ImageIngest
        .ThrowIfNull(nameof(engineOptionsMonitor.CurrentValue.ImageIngest));

    private static readonly AppetiserResponseErrorModel UnknownResponse = new()
    {
        Message = "Unknown response from Appetiser", Status = "503"
    };

    public async Task<IImageProcessorResponse> GenerateJP2(
        IngestionContext context, 
        AssetId modifiedAssetId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<IImageProcessorResponse> GenerateDerivatives(IngestionContext context, AssetId modifiedAssetId,
        IReadOnlyList<SizeParameter> thumbnailSizes, ImageProcessorOperations options,
        CancellationToken cancellationToken = default)
    {
        if (imageIngest.ImageProcessorDelayMs > 0)
        {
            logger.LogTrace("Sleeping for {ImageProcessorDelay}ms", imageIngest.ImageProcessorDelayMs);
            await Task.Delay(imageIngest.ImageProcessorDelayMs, cancellationToken);
        }

        try
        {
            using var request = GetHttpRequest(context, modifiedAssetId, thumbnailSizes, options);
            using var response = await appetiserClient.SendAsync(request, cancellationToken);

            IImageProcessorResponse? responseModel;
            if (response.IsSuccessStatusCode)
            {
                responseModel =
                    await response.Content.ReadFromJsonAsync<AppetiserResponseModel>(
                        cancellationToken: cancellationToken);
            }
            else
            {
                responseModel =
                    await response.Content.ReadFromJsonAsync<AppetiserResponseErrorModel>(
                        cancellationToken: cancellationToken);
            }

            return responseModel ?? UnknownResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while generating Derivatives");
            return new AppetiserResponseErrorModel
            {
                Message = ex.Message, Status = "500"
            };
        }
    }

    private HttpRequestMessage GetHttpRequest(IngestionContext context, AssetId modifiedAssetId,
        IReadOnlyList<SizeParameter> thumbnailSizes, ImageProcessorOperations options)
    {
        var requestModel = CreateModel(context, modifiedAssetId, thumbnailSizes, options);
        var request = new HttpRequestMessage(HttpMethod.Post, "convert");
        request.SetJsonContent(requestModel);
        logger.LogInformation("Calling Appetiser to ingest {AssetId}, JobId {JobId}", context.AssetId,
            requestModel.JobId);
        return request;
    }

    private AppetiserRequestModel CreateModel(IngestionContext context, AssetId modifiedAssetId,
        IReadOnlyList<SizeParameter> thumbnailSizes, ImageProcessorOperations operation) =>
        new()
        {
            Destination = GetJP2FilePath(modifiedAssetId, context.IngestId, true),
            Operation = GetAppetiserOperation(operation),
            Optimisation = "kdu_max",
            Origin = context.Asset.Origin!,
            Source = GetRelativeLocationOnDisk(context, modifiedAssetId),
            ImageId = context.AssetId.Asset,
            ThumbDir = TemplatedFolders.GenerateTemplateForUnix(imageIngest.ThumbsTemplate,
                modifiedAssetId, root: imageIngest.GetRoot(true)),
            JobId = Guid.NewGuid().ToString(),
            ThumbIIIFSize = thumbnailSizes.Select(size => size.ToString()).ToList()
        };
    
    private static string GetAppetiserOperation(ImageProcessorOperations operation)
    {
        const ImageProcessorOperations imageAndThumbs =
            ImageProcessorOperations.Derivative | ImageProcessorOperations.Thumbnails;
        if (operation == ImageProcessorOperations.None)
        {
            throw new InvalidOperationException("You must specify an operation");
        }

        if (operation.HasFlag(imageAndThumbs)) return "ingest";
        if (operation.HasFlag(ImageProcessorOperations.Thumbnails)) return "derivatives-only";
        if (operation.HasFlag(ImageProcessorOperations.Derivative)) return "image-only";
        
        throw new InvalidOperationException("You must specify an operation");
    }

    private string GetRelativeLocationOnDisk(IngestionContext context, AssetId modifiedAssetId)
    {
        var assetOnDisk = context.AssetFromOrigin!.Location;
        var extension = assetOnDisk.EverythingAfterLast('.');

        // this is to get it working nice locally as appetiser/tizer root needs to be unix + relative to it
        var imageProcessorRoot = ImageIngestionHelpers.GetWorkingFolder(context.IngestId, imageIngest, true);
        var unixPath = TemplatedFolders.GenerateTemplateForUnix(imageIngest.SourceTemplate, modifiedAssetId,
            root: imageProcessorRoot);

        unixPath += $"/{modifiedAssetId.Asset}.{extension}";
        return unixPath;
    }
    
    public string GetJP2FilePath(AssetId assetId, string ingestId, bool forImageProcessor)
    {
        // Appetiser/Tizer want unix paths relative to mount share.
        // This logic allows handling when running locally on win/unix and when deployed to unix
        var destFolder = forImageProcessor
            ? TemplatedFolders.GenerateTemplateForUnix(imageIngest.DestinationTemplate,
                assetId, root: ImageIngestionHelpers.GetWorkingFolder(ingestId, imageIngest, true))
            : TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate,
                assetId, root: ImageIngestionHelpers.GetWorkingFolder(ingestId, imageIngest));

        return $"{destFolder}{assetId.Asset}.jp2";
    }
}
