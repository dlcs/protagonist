using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
using DLCS.Repository.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Zip
{
    /// <summary>
    /// Project assets to Zip archive.
    /// </summary>
    public class ZipCreator : BaseProjectionCreator<ZipParsedNamedQuery>
    {
        public ZipCreator(IBucketReader bucketReader, IOptions<NamedQuerySettings> namedQuerySettings,
            ILogger<ZipCreator> logger) :
            base(bucketReader, namedQuerySettings, logger)
        {
        }

        protected override async Task<CreateProjectionResult> CreateFile(ZipParsedNamedQuery parsedNamedQuery,
            List<Asset> assets)
        {
            var storageKey = parsedNamedQuery.StorageKey;
            var zipFilePath = GetZipFilePath(parsedNamedQuery);

            try
            {
                DeleteZipFileIfExists(zipFilePath);
                await CreateZipFileOnDisk(parsedNamedQuery, assets, storageKey, zipFilePath);

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
            var objectInBucket = new ObjectInBucket(NamedQuerySettings.OutputBucket, parsedNamedQuery.StorageKey);
            var success = await BucketReader.WriteFileToBucket(objectInBucket, zipFilePath, "application/zip");
            var fileInfo = new FileInfo(zipFilePath);

            return new CreateProjectionResult
            {
                Size = fileInfo.Length,
                Success = success
            };
        }

        private async Task CreateZipFileOnDisk(ZipParsedNamedQuery parsedNamedQuery, List<Asset> assets, string? storageKey,
            string? zipFilePath)
        {
            Logger.LogInformation("Creating new zip archive at {S3Key} with {AssetCount} assets",
                storageKey, assets.Count);

            await using var zipToOpen = new FileStream(zipFilePath, FileMode.Create);
            using var zipArchive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);

            foreach (var i in assets.OrderBy(i =>
                NamedQueryProjections.GetCanvasOrderingElement(i, parsedNamedQuery)))
            {
                await ProcessImage(i, storageKey, zipArchive);
            }
        }

        private async Task ProcessImage(Asset image, string? storageKey, ZipArchive zipArchive)
        {
            if (image.RequiresAuth)
            {
                Logger.LogDebug("Image {Image} of {S3Key} has roles, redacting", image.Id, storageKey);
                return;
            }

            var largestThumb = new ObjectInBucket(NamedQuerySettings.ThumbsBucket,
                $"{image.GetStorageKey()}/low.jpg");
            var largestThumbStream = await BucketReader.GetObjectContentFromBucket(largestThumb);
            if (largestThumbStream == null || largestThumbStream == Stream.Null)
            {
                Logger.LogWarning("Could not find largest thumb for {Image} of {S3Key}", image.Id, storageKey);
                return;
            }

            var archiveEntry = zipArchive.CreateEntry($"{image.GetUniqueName()}.jpg");
            await using var archiveEntryStream = archiveEntry.Open();
            await largestThumbStream.CopyToAsync(archiveEntryStream);
        }

        private static void DeleteZipFileIfExists(string zipFilePath)
        {
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
        }

        // TODO - where should this go?
        private static string GetZipFilePath(ZipParsedNamedQuery parsedNamedQuery) 
            => Path.Join(Path.GetTempPath(), $"{parsedNamedQuery.StorageKey}.zip");
    }
}