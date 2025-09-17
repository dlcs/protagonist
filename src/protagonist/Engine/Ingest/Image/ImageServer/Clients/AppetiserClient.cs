using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Templates;
using DLCS.Web.Requests;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer.Clients;

public class AppetiserClient : IImageProcessorClient
{
    private readonly HttpClient appetiserClient;
    private readonly EngineSettings engineSettings;

    public AppetiserClient(
        HttpClient appetiserClient,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor)
    {
        this.appetiserClient = appetiserClient;
        engineSettings = engineOptionsMonitor.CurrentValue;
    }
    
    public async Task<IAppetiserResponse> GenerateJP2(
        IngestionContext context, 
        AssetId modifiedAssetId,
        CancellationToken cancellationToken = default)
    {
        var requestModel = CreateModel(context, modifiedAssetId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "convert");
        IAppetiserResponse? responseModel;
        request.SetJsonContent(requestModel);

        if (engineSettings.ImageIngest.ImageProcessorDelayMs > 0)
        {
            await Task.Delay(engineSettings.ImageIngest.ImageProcessorDelayMs);
        }

        using var response = await appetiserClient.SendAsync(request, cancellationToken);

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

        return responseModel;
    }
    
    private AppetiserRequestModel CreateModel(IngestionContext context, AssetId modifiedAssetId)
    {
        var requestModel = new AppetiserRequestModel
        {
            Destination = GetJP2FilePath(modifiedAssetId, context.IngestId, true),
            Operation = "image-only",
            Optimisation = "kdu_max",
            Origin = context.Asset.Origin,
            Source = GetRelativeLocationOnDisk(context, modifiedAssetId),
            ImageId = context.AssetId.Asset,
            JobId = Guid.NewGuid().ToString(),
        };

        return requestModel;
    }
    
    private string GetRelativeLocationOnDisk(IngestionContext context, AssetId modifiedAssetId)
    {
        var assetOnDisk = context.AssetFromOrigin.Location;
        var extension = assetOnDisk.EverythingAfterLast('.');

        // this is to get it working nice locally as appetiser/tizer root needs to be unix + relative to it
        
        var imageProcessorRoot = ImageIngestionHelpers.GetWorkingFolder(context.IngestId, engineSettings.ImageIngest, true);
        var unixPath = TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.SourceTemplate, modifiedAssetId,
            root: imageProcessorRoot);

        unixPath += $"/{modifiedAssetId.Asset}.{extension}";
        return unixPath;
    }
    
    public string GetJP2FilePath(AssetId assetId, string ingestId, bool forImageProcessor)
    {
        // Appetiser/Tizer want unix paths relative to mount share.
        // This logic allows handling when running locally on win/unix and when deployed to unix
        var destFolder = forImageProcessor
            ? TemplatedFolders.GenerateTemplateForUnix(engineSettings.ImageIngest.DestinationTemplate,
                assetId, root: ImageIngestionHelpers.GetWorkingFolder(ingestId, engineSettings.ImageIngest, true))
            : TemplatedFolders.GenerateFolderTemplate(engineSettings.ImageIngest.DestinationTemplate,
                assetId, root: ImageIngestionHelpers.GetWorkingFolder(ingestId, engineSettings.ImageIngest));

        return $"{destFolder}{assetId.Asset}.jp2";
    }
}
