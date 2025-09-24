using System.Net;
using System.Text.Json;
using DLCS.Core.Collections;
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
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly AppetiserResponseErrorModel UnknownResponse = new()
    {
        Message = "Unknown response from Appetiser", Status = 503
    };

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
                    await response.Content.ReadFromJsonAsync<AppetiserResponseModel>(cancellationToken);
                if (responseModel is AppetiserResponseModel appetiserResponse)
                {
                    SetRelativePaths(context, modifiedAssetId, appetiserResponse);
                }
            }
            else
            {
                responseModel = await GetAppetiserResponseErrorModel(response, cancellationToken);
            }

            return responseModel ?? UnknownResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while generating Derivatives");
            return new AppetiserResponseErrorModel
            {
                Message = ex.Message, Status = 500
            };
        }
    }
    
    private HttpRequestMessage GetHttpRequest(IngestionContext context, AssetId assetId,
        IReadOnlyList<SizeParameter> thumbnailSizes, ImageProcessorOperations options)
    {
        var requestModel = CreateModel(context, assetId, thumbnailSizes, options);
        var request = new HttpRequestMessage(HttpMethod.Post, "convert");
        request.SetJsonContent(requestModel);
        logger.LogDebug("Calling Appetiser to ingest {AssetId}, JobId {JobId}", context.AssetId,
            requestModel.JobId);
        return request;
    }

    private AppetiserRequestModel CreateModel(IngestionContext context, AssetId assetId,
        IReadOnlyList<SizeParameter> thumbnailSizes, ImageProcessorOperations operation) =>
        new()
        {
            Destination = GetImageServerAwarePath(assetId, context.IngestId, true, TemplateType.Destination, "jp2"),
            Operation = GetAppetiserOperation(operation),
            Optimisation = "kdu_max",
            Origin = context.Asset.Origin!,
            Source = GetImageServerAwarePath(assetId, context.IngestId, true, TemplateType.Source,
                context.AssetFromOrigin!.Location.EverythingAfterLast('.')),
            ImageId = context.AssetId.Asset,
            ThumbDir = GetImageServerAwarePath(assetId, context.IngestId, true, TemplateType.Thumbs),
            JobId = Guid.NewGuid().ToString(),
            ThumbIIIFSizes = thumbnailSizes.Select(size => size.ToString()).ToList()
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
    
    private void SetRelativePaths(IngestionContext context, AssetId assetId, AppetiserResponseModel response)
    {
        logger.LogDebug("Setting relative paths on return model");
        // Update the location of all thumbs to be full path on disk, relative to orchestrator
        var partialThumbTemplate = GetImageServerAwarePath(assetId, context.IngestId, false, TemplateType.Thumbs);
        foreach (var thumb in response.Thumbs)
        {
            var key = thumb.Path.EverythingAfterLast('/');
            thumb.Path = string.Concat(partialThumbTemplate, key);
        }
        
        // and set the output location of the JP2, relative to orchestrator
        if (!string.IsNullOrEmpty(response.JP2))
        {
            var partialImageTemplate =
                GetImageServerAwarePath(assetId, context.IngestId, false, TemplateType.Destination);
            var key = response.JP2.EverythingAfterLast('/');
            response.JP2 = string.Concat(partialImageTemplate, key);
        } 
    }

    /// <summary>
    /// Attempt to simplify logic for building paths. Appetiser needs unix paths relative to mount share but
    /// Orchestrator will want relative paths dependant on OS it's running on.
    /// 
    /// This logic allows handling when running locally on win/unix and when deployed to unix
    /// </summary>
    /// <param name="assetId">Modified assetId</param>
    /// <param name="ingestId">Ingest identifier, for avoiding overwriting files</param>
    /// <param name="forAppetiser">True if the generated path will be used by Appetiser</param>
    /// <param name="templateType">Type of template to use</param>
    /// <param name="extensionForFile">Leave null if want directory only. If supplied then result will be
    /// "{destFolder}{assetId.Asset}.{extensionForFile}"</param>
    /// <returns></returns>
    private string GetImageServerAwarePath(AssetId assetId, string ingestId, bool forAppetiser, TemplateType templateType,
        string? extensionForFile = null)
    {
        var template = templateType switch
        {
            TemplateType.Destination =>  imageIngest.DestinationTemplate,
            TemplateType.Thumbs =>  imageIngest.ThumbsTemplate,
            TemplateType.Source =>  imageIngest.SourceTemplate,
            _ => throw new InvalidOperationException($"Unknown template type: {templateType}")
        };
        
        var destFolder = forAppetiser
            ? TemplatedFolders.GenerateTemplateForUnix(template,
                assetId, root: ImageIngestionHelpers.GetWorkingFolder(ingestId, imageIngest, true))
            : TemplatedFolders.GenerateFolderTemplate(template,
                assetId, root: ImageIngestionHelpers.GetWorkingFolder(ingestId, imageIngest));

        var separator = forAppetiser ? '/' : Path.DirectorySeparatorChar;
        if (destFolder.Last() != separator) destFolder += separator; 

        // If forThumb then we just want a folder, else we want path to actual jp2
        return extensionForFile == null ? destFolder : $"{destFolder}{assetId.Asset}.{extensionForFile}";
    }

    private enum TemplateType
    {
        Source,
        Thumbs,
        Destination
    };

    private async Task<AppetiserResponseErrorModel> GetAppetiserResponseErrorModel(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        // Appetiser returns a different shape of response depending on whether it's a 422 or another exception
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var fullResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Appetiser request failed validation, full response: {@ValidationResponse}", fullResponse);
            var validationError =
                JsonSerializer.Deserialize<AppetiserValidationError>(fullResponse, JsonSerializerOptions);
            if (validationError != null)
            {
                return new AppetiserResponseErrorModel
                {
                    Message = validationError.Summarise(),
                    Status = (int)response.StatusCode
                };
            }
        }
        else
        {
            var generalError = await response.Content.ReadFromJsonAsync<AppetiserGeneralError>(cancellationToken);
            if (generalError != null)
            {
                return new AppetiserResponseErrorModel
                {
                    Message = generalError.Detail ?? UnknownResponse.Message,
                    Status = (int)response.StatusCode
                };
            }
        }

        return UnknownResponse;
    }

    /// <summary>
    /// Appetiser validation exceptions are a different shape to all other exceptions
    /// </summary>
    private class AppetiserValidationError
    {
        public IEnumerable<ValidationDetail>? Detail { get; set; }

        public string Summarise() =>
            Detail.IsNullOrEmpty()
                ? "Appetiser Validation error"
                : string.Join(". ", Detail.Select(d => d.ToString()));

        /// <summary>
        /// Detail of a validation error. We get more fields than this but we only map these as they help create
        /// the final Message.
        /// </summary>
        /// <summary>
        /// E.g. if payload { "file": "/path/to/file.jpeg" } failed validation then resulting props would be:
        /// 
        ///   Loc = ["body", "file"]
        ///   Input = "/path/to/file.jpeg"
        ///   Msg = "Path does not point to a file"
        ///
        /// </summary>
        public class ValidationDetail
        {
            /// <summary>
            /// The property that is failing validation. This is an array of properties, always starting "body".
            /// </summary>
            public string[] Loc { get; set; } = [];
            
            /// <summary>
            /// The value the validation message is concerning
            /// </summary>
            public string Input { get; set; } = "";

            /// <summary>
            /// The value the validation message is concerning
            /// </summary>
            public string Msg { get; set; } = "";

            public override string ToString()
            {
                var prop = Loc.Length == 1 ? Loc[0] : string.Join(">", Loc[1..]);
                return $"{prop}: {Input} - {Msg}";
            }
        }
    }

    /// <summary>
    /// All non-validation exceptions will contain a single "detail" property
    /// </summary>
    private class AppetiserGeneralError
    {
        public string? Detail { get; set; }
    }
}
