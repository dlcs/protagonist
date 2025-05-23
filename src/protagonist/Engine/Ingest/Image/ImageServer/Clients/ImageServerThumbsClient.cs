﻿using System.Diagnostics;
using System.Net;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using Engine.Ingest.Image.ImageServer.Measuring;
using Engine.Settings;
using IIIF;
using IIIF.ImageApi;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Engine.Ingest.Image.ImageServer.Clients;

/// <summary>
/// Implementation of <see cref="IThumbsClient"/> using IIIF ImageServer (e.g. Cantaloupe or Laya) for generation
/// </summary>
public class ImageServerThumbsClient : IThumbsClient
{
    private readonly HttpClient imageServerClient;
    private readonly IFileSystem fileSystem;
    private readonly IImageMeasurer imageMeasurer;
    private readonly ILogger<ImageServerThumbsClient> logger;
    private List<string> loadBalancerCookies = new();
    private readonly EngineSettings engineSettings;

    public ImageServerThumbsClient(
        HttpClient imageServerClient,
        IFileSystem fileSystem,
        IImageMeasurer imageMeasurer,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor,
        ILogger<ImageServerThumbsClient> logger)
    {
        this.imageServerClient = imageServerClient;
        this.fileSystem = fileSystem;
        this.imageMeasurer = imageMeasurer;
        this.logger = logger;
        engineSettings = engineOptionsMonitor.CurrentValue;
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
            var sw = Stopwatch.StartNew();
            ++count;
            var imageOnDisk = await GenerateSingleThumbnail(thumbFolder, convertedS3Location, size, assetId, count,
                thumbsResponse, imageSize, true, cancellationToken);
            
            if (imageOnDisk is not null)
            {
                thumbsResponse.Add(imageOnDisk);
                ValidateSize(size, imageSize, imageOnDisk, assetId);
            }
            
            sw.Stop();
            logger.LogTrace("Processed thumb {ThumbSize} for {AssetId} in {Elapsed}ms", size, assetId,
                sw.ElapsedMilliseconds);
        }
        
        return thumbsResponse;
    }

    private async Task<ImageOnDisk?> GenerateSingleThumbnail(string thumbFolder,
        string convertedS3Location, string size, AssetId assetId, int count, List<ImageOnDisk> thumbsResponse,
        Size imageSize, bool shouldRetry, CancellationToken cancellationToken)
    {
        var request = CreateRequestMessage(convertedS3Location, size);

        using var response = await imageServerClient.SendAsync(request, cancellationToken);
        
        AttemptToAddStickinessCookie(response);
        
        if (response.StatusCode == HttpStatusCode.BadRequest &&
            await IsErrorDueToIncorrectImageRequest(response, cancellationToken))
        {
            await LogErrorResponse(response, assetId, size, LogLevel.Information, cancellationToken);
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            return await SaveImageToDisk(response, size, thumbFolder, count, shouldRetry, convertedS3Location,
                assetId, thumbsResponse, imageSize, cancellationToken);
        }

        await LogErrorResponse(response, assetId, size, LogLevel.Error, cancellationToken);
        throw new HttpException(response.StatusCode, "Failed to retrieve data from the thumbs processor");
    }

    // Collection of known values found in 400 response body which signify error can be ignored
    private static readonly string[] KnownIgnorableBadRequests = new[]
    {
        "scales in excess of 100%", // Cantaloupe. For when size-param exceeds image dimensions and ^ not used
        "image dimensions exceed largest", // Laya. For when size-param exceeds image dimensions and ^ not used
    };

    private static async Task<bool> IsErrorDueToIncorrectImageRequest(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return KnownIgnorableBadRequests.Any(upscale => body.Contains(upscale));
    }

    private HttpRequestMessage CreateRequestMessage(string convertedS3Location, string size)
    {
        // use a dot-segment (./) to be explicit that this is a relative path or the colon in s3: makes http-client
        // this it's absolute. see https://github.com/dotnet/runtime/issues/24266#issuecomment-347604913
        var request = new HttpRequestMessage(HttpMethod.Get, $"./{convertedS3Location}/full/{size}/0/default.jpg");

        if (loadBalancerCookies.Any())
        {
            request.Headers.Add(HeaderNames.Cookie, loadBalancerCookies);
        }

        return request;
    }

    private void AttemptToAddStickinessCookie(HttpResponseMessage response)
    {
        var hasCookie = response.Headers.TryGetValues(HeaderNames.SetCookie, out var cookies);
        if (hasCookie)
        {
            loadBalancerCookies = new List<string>();
            foreach (var cookie in cookies!)
            {
                if (engineSettings.ImageIngest!.LoadBalancerStickinessCookieNames.Any(c =>
                        cookie.Split(';').Any(h => h.Trim(' ').StartsWith($"{c}="))))
                {
                    loadBalancerCookies.Add(cookie);
                }
            }
        }
    }

    private async Task<ImageOnDisk> SaveImageToDisk(HttpResponseMessage response, string size, string thumbFolder,
        int count, bool shouldRetry, string convertedS3Location, AssetId assetId, List<ImageOnDisk> thumbsResponse,
        Size imageSize, CancellationToken cancellationToken)
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var localThumbsPath = Path.Join(thumbFolder, $"thumb{count}");
        logger.LogTrace("Saving thumb for {ThumbSize} to {ThumbLocation}", size, localThumbsPath);
                
        await fileSystem.CreateFileFromStream(localThumbsPath, responseStream, cancellationToken);
                
        var imageOnDisk = await imageMeasurer.MeasureImage(localThumbsPath, cancellationToken);

        return (imageOnDisk switch
        {
            null when shouldRetry => await GenerateSingleThumbnail(thumbFolder, convertedS3Location, size, assetId, count,
                thumbsResponse, imageSize, false, cancellationToken),
            null when !shouldRetry => throw new InvalidOperationException("Failed to measure image on disk"),
            _ => imageOnDisk
        })!;
    }

    private async Task LogErrorResponse(HttpResponseMessage response, AssetId assetId, string size, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.Log(logLevel,
            "ImageServer responded with status code {StatusCode} when processing Asset {AssetId}, size '{Size}' and body {ErrorResponse}",
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
        }
        return;

        void HandleMismatch(bool allowed)
        {
            if (allowed)
            {
                logger.LogTrace(
                    "Size mismatch for {AssetId}, size '{Size}'. Expected:'{Expected}', actual:'{Actual}'",
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
