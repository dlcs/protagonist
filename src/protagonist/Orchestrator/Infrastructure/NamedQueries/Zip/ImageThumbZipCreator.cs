using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Streams;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Zip;

/// <summary>
/// Project assets to Zip archive.
/// </summary>
public class ImageThumbZipCreator : BaseProjectionCreator<ZipParsedNamedQuery>
{
    private readonly IThumbSizeProvider thumbSizeProvider;
    public ImageThumbZipCreator(
        IBucketReader bucketReader, 
        IBucketWriter bucketWriter,
        IThumbSizeProvider thumbSizeProvider,
        IOptions<NamedQuerySettings> namedQuerySettings, 
        IStorageKeyGenerator storageKeyGenerator,
        ILogger<ImageThumbZipCreator> logger) :
        base(bucketReader, bucketWriter, namedQuerySettings, storageKeyGenerator, logger)
    {
        this.thumbSizeProvider = thumbSizeProvider;
    }

    protected override async Task<CreateProjectionResult> CreateFile(ZipParsedNamedQuery parsedNamedQuery,
        List<Asset> assets, CancellationToken cancellationToken)
    {
        var storageKey = parsedNamedQuery.StorageKey;
        var zipFilePath = GetZipFilePath(parsedNamedQuery);
        Logger.LogInformation("Creating new zip document at {ZipS3Key}", storageKey);

        try
        {
            // NOTE - if another process is working on this zip file this will throw an exception
            DeleteZipFileIfExists(zipFilePath);
            await CreateZipFileOnDisk(parsedNamedQuery, assets, storageKey, zipFilePath, cancellationToken);

            return await UploadZipToS3(parsedNamedQuery, zipFilePath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unknown exception creating Zip archive with NQ request: {S3Key}", storageKey);
        }
        finally
        {
            DeleteZipFileIfExists(zipFilePath);
        }

        return new CreateProjectionResult();
    }

    private async Task<CreateProjectionResult> UploadZipToS3(ZipParsedNamedQuery parsedNamedQuery, string zipFilePath)
    {
        var destination = StorageKeyGenerator.GetOutputLocation(parsedNamedQuery.StorageKey);
        Logger.LogDebug("Uploading new zip archive to {S3Key}", destination);
        var success = await BucketWriter.WriteFileToBucket(destination, zipFilePath, "application/zip");
        var fileInfo = new FileInfo(zipFilePath);

        return new CreateProjectionResult
        {
            Size = fileInfo.Length,
            Success = success
        };
    }

    private async Task CreateZipFileOnDisk(ZipParsedNamedQuery parsedNamedQuery, List<Asset> assets,
        string storageKey, string zipFilePath, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Creating new zip archive for {S3Key} at {LocalPath} with {AssetCount} assets",
            storageKey, zipFilePath, assets.Count);

        Directory.CreateDirectory(zipFilePath[..zipFilePath.LastIndexOf(Path.DirectorySeparatorChar)]);
        await using var zipToOpen = new FileStream(zipFilePath, FileMode.Create);
        using var zipArchive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);

        int imageCount = 0;
        foreach (var i in NamedQueryProjections.GetOrderedAssets(assets, parsedNamedQuery))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning("Creation of zip file at {LocalPath} cancelled, aborting", zipFilePath);
                cancellationToken.ThrowIfCancellationRequested();
            }
            Logger.LogTrace("Adding image {Image} to {LocalPath}", ++imageCount, zipFilePath);
            await ProcessImage(i, storageKey, zipArchive);
        }
    }

    private async Task ProcessImage(Asset image, string? storageKey, ZipArchive zipArchive)
    {
        if (image.RequiresAuth)
        {
            Logger.LogDebug("Image {Image} of {S3Key} requires auth, redacting", image.Id, storageKey);
            return;
        }

        var thumbStream = await GetThumbnailStream(image);
        if (thumbStream.IsNull())
        {
            Logger.LogWarning("Could not find thumb for {Image} of {S3Key}", image.Id, storageKey);
            return;
        }

        var archiveEntry = zipArchive.CreateEntry($"{image.Id.Asset}.jpg");
        await using var archiveEntryStream = archiveEntry.Open();
        await thumbStream.CopyToAsync(archiveEntryStream);
    }

    private static void DeleteZipFileIfExists(string zipFilePath)
    {
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }
    }

    private string GetZipFilePath(ZipParsedNamedQuery parsedNamedQuery)
    {
        var pathSafeStorageKey = $"{parsedNamedQuery.StorageKey}".Replace('/', '_');
        return NamedQuerySettings.ZipFolderTemplate
            .Replace("{customer}", parsedNamedQuery.Customer.ToString())
            .Replace("{storage-key}", pathSafeStorageKey);
    }
    
    private async Task<Stream?> GetThumbnailStream(Asset asset)
    {
        var availableSizes = await thumbSizeProvider.GetThumbSizesForImage(asset);
        
        if (availableSizes.IsEmpty()) return null;
        
        var selectedSize = availableSizes.SizeClosestTo(NamedQuerySettings.ProjectionThumbsize, out var isOpen);
        Logger.LogTrace("Using thumbnail {ThumbnailSize} for asset {AssetId}. IsOpen: {ThumbnailOpen}", selectedSize,
            asset.Id, isOpen);
        var thumbnailLocation = StorageKeyGenerator.GetThumbnailLocation(asset.Id, selectedSize.MaxDimension, isOpen);
        
        var thumbStream = await BucketReader.GetObjectContentFromBucket(thumbnailLocation);
        return thumbStream;
    }
}