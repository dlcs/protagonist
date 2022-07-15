﻿using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Thumbs;
using DLCS.Model.Customers;
using DLCS.Model.Templates;
using DLCS.Repository.Entities;
using DLCS.Web.Requests;
using Engine.Ingest.Workers;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image;

/// <summary>
/// Generates image derivatives and converts source image to alternative format. 
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Generate thumbnails and/or tile-optimised image file.
    /// Copy generated files to destination 'slow' storage, if appropriate. 
    /// </summary>
    /// <param name="context">Object representing current ingestion operation.</param>
    /// <returns>true if succeeded, else false.</returns>
    Task<bool> ProcessImage(IngestionContext context);
}

/// <summary>
/// Derivative generator using Appetiser for generating resources
/// </summary>
public class AppetiserClient : IImageProcessor
{
    private readonly HttpClient httpClient;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<AppetiserClient> logger;
    private readonly IBucketWriter bucketWriter;

    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IThumbLayoutManager thumbLayoutManager;

    public AppetiserClient(
        HttpClient httpClient,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        IThumbLayoutManager thumbLayoutManager,
        IOptionsMonitor<EngineSettings> engineOptionsMonitor,
        ILogger<AppetiserClient> logger)
    {
        this.httpClient = httpClient;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
        this.thumbLayoutManager = thumbLayoutManager;
        engineSettings = engineOptionsMonitor.CurrentValue;
        this.logger = logger;
    }

    public async Task<bool> ProcessImage(IngestionContext context)
    {
        EnsureRequiredFoldersExist(context.Asset);

        try
        {
            var derivativesOnly = IsDerivativesOnly(context.AssetFromOrigin);
            var responseModel = await CallImageProcessor(context, derivativesOnly);
            var (imageLocation, imageStorage) = await ProcessResponse(context, responseModel, derivativesOnly);
            context.WithLocation(imageLocation).WithStorage(imageStorage);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing image {asset}", context.Asset.Id);
            context.Asset.Error = e.Message;
            return false;
        }
    }

    private void EnsureRequiredFoldersExist(Asset asset)
    {
        var imageIngest = engineSettings.ImageIngest;
        var root = imageIngest.GetRoot();

        var assetId = asset.GetAssetId();

        // dest is the folder where appetiser will copy output to
        var dest = TemplatedFolders.GenerateFolderTemplate(imageIngest.DestinationTemplate, assetId, root: root);

        // thumb is the folder where generated thumbnails will be output to
        var thumb = TemplatedFolders.GenerateFolderTemplate(imageIngest.ThumbsTemplate, assetId, root: root);

        Directory.CreateDirectory(dest);
        Directory.CreateDirectory(thumb);
    }

    // If image is already a JPEG2000, only generate derivatives
    private static bool IsDerivativesOnly(AssetFromOrigin assetFromOrigin)
        => assetFromOrigin.ContentType is MIMEHelper.JP2 or MIMEHelper.JPX;

    private async Task<AppetiserResponseModel> CallImageProcessor(IngestionContext context,
        bool derivativesOnly)
    {
        // call tizer/appetiser
        var requestModel = CreateModel(context, derivativesOnly);

        using var request = new HttpRequestMessage(HttpMethod.Post, (Uri)null);
        request.SetJsonContent(requestModel);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // TODO - it's possible to get a 200 when appetiser doesn't do anything, e.g. body not understood
        var responseModel = await response.Content.ReadFromJsonAsync<AppetiserResponseModel>();
        return responseModel;
    }

    private AppetiserRequestModel CreateModel(IngestionContext context, bool derivativesOnly)
    {
        var asset = context.Asset;
        var imageOptimisationPolicy = asset.FullImageOptimisationPolicy;
        if (imageOptimisationPolicy.TechnicalDetails.Length > 1)
        {
            logger.LogWarning(
                "ImageOptimisationPolicy {PolicyId} has {TechDetailsCount} technicalDetails but we can only handle 1",
                imageOptimisationPolicy.Id, imageOptimisationPolicy.TechnicalDetails.Length);
        }

        var requestModel = new AppetiserRequestModel
        {
            Destination = GetJP2File(context.AssetId, true),
            Operation = derivativesOnly ? "derivatives-only" : "ingest",
            Optimisation = imageOptimisationPolicy.TechnicalDetails.FirstOrDefault() ?? string.Empty,
            Origin = asset.Origin,
            Source = GetRelativeLocationOnDisk(context),
            ImageId = asset.GetUniqueName(),
            JobId = Guid.NewGuid().ToString(),
            ThumbDir = TemplatedFolders.GenerateTemplate(engineSettings.ImageIngest.ThumbsTemplate,
                context.AssetId, "/", root: engineSettings.ImageIngest.GetRoot(true)),
            ThumbSizes = asset.FullThumbnailPolicy.SizeList
        };

        return requestModel;
    }

    private string GetJP2File(AssetId assetId, bool forImageProcessor)
    {
        // Appetiser/Tizer want unix paths relative to mount share.
        // This logic allows handling when running locally on win/unix and when deployed to unix
        var destFolder = forImageProcessor
            ? TemplatedFolders.GenerateTemplate(engineSettings.ImageIngest.DestinationTemplate,
                assetId, "/", root: engineSettings.ImageIngest.GetRoot(true))
            : TemplatedFolders.GenerateFolderTemplate(engineSettings.ImageIngest.DestinationTemplate,
                assetId, root: engineSettings.ImageIngest.GetRoot());

        return $"{destFolder}{assetId.Asset}.jp2";
    }

    private string GetRelativeLocationOnDisk(IngestionContext context)
    {
        var assetOnDisk = context.AssetFromOrigin.Location;
        var extension = assetOnDisk.EverythingAfterLast('.');

        // this is to get it working nice locally as appetiser/tizer root needs to be unix + relative to it
        var imageProcessorRoot = engineSettings.ImageIngest.GetRoot(true);
        var unixPath = TemplatedFolders.GenerateTemplate(engineSettings.ImageIngest.SourceTemplate, context.AssetId,
            "/", root: imageProcessorRoot);

        unixPath += $"/{context.Asset.GetUniqueName()}.{extension}";
        return unixPath;
    }

    private async Task<(ImageLocation imageLocation, ImageStorage imageStorage)> ProcessResponse(
        IngestionContext context, AppetiserResponseModel responseModel, bool derivativesOnly)
    {
        UpdateImageSize(context.Asset, responseModel);

        var imageLocation = await ProcessOriginImage(context, derivativesOnly);

        await CreateNewThumbs(context, responseModel);

        ImageStorage imageStorage = GetImageStorage(context, responseModel);

        return (imageLocation, imageStorage);
    }

    private static void UpdateImageSize(Asset asset, AppetiserResponseModel responseModel)
    {
        asset.Height = responseModel.Height;
        asset.Width = responseModel.Width;
    }

    private async Task<ImageLocation> ProcessOriginImage(IngestionContext context, bool derivativesOnly)
    {
        var asset = context.Asset;
        var imageLocation = new ImageLocation { Id = asset.Id };

        var originStrategy = context.AssetFromOrigin.CustomerOriginStrategy;

        if (originStrategy.Optimised && originStrategy.Strategy == OriginStrategyType.S3Ambient)
        {
            // Optimised strategy - we don't want to store as we've not created a new version - just set imageLocation
            var originObject = RegionalisedObjectInBucket.Parse(asset.GetIngestOrigin());
            storageKeyGenerator.EnsureRegionSet(originObject);

            imageLocation.S3 = originObject.GetS3Uri().ToString();
            return imageLocation;
        }

        if (originStrategy.Optimised)
        {
            logger.LogWarning("Asset {AssetId} has originStrategy '{OriginStrategy}', which is optimised but not S3",
                context.AssetId, originStrategy.Id);
        }

        var jp2BucketObject = storageKeyGenerator.GetStorageLocation(context.AssetId);

        // if derivatives-only, no new JP2 will have been generated so use the 'origin' file
        var jp2File = derivativesOnly ? context.AssetFromOrigin.Location : GetJP2File(context.AssetId, false);

        // Not optimised - upload JP2 to S3 and set ImageLocation to new bucket location
        if (!await bucketWriter.WriteFileToBucket(jp2BucketObject, jp2File, MIMEHelper.JP2))
        {
            throw new ApplicationException($"Failed to write jp2 {jp2File} to storage bucket");
        }

        imageLocation.S3 = jp2BucketObject.GetS3Uri().ToString();
        return imageLocation;
    }

    private async Task CreateNewThumbs(IngestionContext context, AppetiserResponseModel responseModel)
    {
        SetThumbsOnDiskLocation(context, responseModel);

        await thumbLayoutManager.CreateNewThumbs(context.Asset, responseModel.Thumbs.ToList());
    }

    private void SetThumbsOnDiskLocation(IngestionContext context, AppetiserResponseModel responseModel)
    {
        // Update the location of all thumbs to be full path on disk.
        var partialTemplate = TemplatedFolders.GenerateFolderTemplate(engineSettings.ImageIngest.ThumbsTemplate,
            context.AssetId, root: engineSettings.ImageIngest.GetRoot());
        foreach (var thumb in responseModel.Thumbs)
        {
            var key = thumb.Path.EverythingAfterLast('/');
            thumb.Path = string.Concat(partialTemplate, key);
        }
    }
    
    private ImageStorage GetImageStorage(IngestionContext context, AppetiserResponseModel responseModel)
    {
        var asset = context.Asset;

        long GetFileSize(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return fi.Length;
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Error getting fileSize for {Path}", path);
                return 0;
            }
        }

        var thumbSizes = responseModel.Thumbs.Sum(t => GetFileSize(t.Path));

        return new ImageStorage
        {
            Id = asset.Id,
            Customer = asset.Customer,
            Space = asset.Space,
            LastChecked = DateTime.UtcNow,
            Size = context.AssetFromOrigin.AssetSize,
            ThumbnailSize = thumbSizes
        };
    }
}

/// <summary>
/// Response model for receiving requests back from Appetiser.
/// </summary>
public class AppetiserResponseModel
{
    public string ImageId { get; set; }
    public string JobId { get; set; }
    public string Optimisation { get; set; }
    public string JP2 { get; set; }
    public string Origin { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public string InfoJson { get; set; }
    public IEnumerable<ImageOnDisk> Thumbs { get; set; }
}

/// <summary>
/// Request model for making requests to Appetiser.
/// </summary>
public class AppetiserRequestModel
{
    public string ImageId { get; set; }
    public string JobId { get; set; }
    public string Source { get; set; }
    public string Destination { get; set; }
    public string ThumbDir { get; set; }
    public IEnumerable<int> ThumbSizes { get; set; }
    public string Optimisation { get; set; }
    public string Operation { get; set; }
    public string Origin { get; set; }
}