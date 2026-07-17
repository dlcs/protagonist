using System.Text.Json;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Streams;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy.Utils;
using Engine.Ingest.Persistence;

namespace Engine.Ingest.File;

/// <summary>
/// <see cref="IAssetIngester"/> implementation for handling "file" delivery-channel
/// </summary>
public class FileChannelWorker(
    IAssetToS3 assetToS3,
    IAssetIngestorSizeCheck assetIngestorSizeCheck,
    IStorageKeyGenerator storageKeyGenerator,
    OriginFetcher originFetcher,
    IBucketReader bucketReader,
    ILogger<FileChannelWorker> logger)
    : IAssetIngesterWorker, IAdjunctIngesterWorker
{
    public async Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var asset = ingestionContext.Asset;

        try
        {
            if (customerOriginStrategy.Optimised)
            {
                logger.LogDebug("Asset {Asset} is at optimised origin, no 'file' handling required",
                    ingestionContext.AssetId);
                return IngestResultStatus.Success;
            }

            var targetStorageLocation = storageKeyGenerator.GetStoredOriginalLocation(ingestionContext.AssetId);

            var assetInBucket = await assetToS3.CopyOriginToStorage(targetStorageLocation,
                ingestionContext,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer),
                customerOriginStrategy,
                cancellationToken: cancellationToken);

            ingestionContext.WithAssetFromOrigin(assetInBucket);

            if (assetIngestorSizeCheck.DoesAssetFromOriginExceedAllowance(assetInBucket, asset))
            {
                return IngestResultStatus.StorageLimitExceeded;
            }

            UpdateIngestionContext(ingestionContext, assetInBucket, targetStorageLocation);
            return IngestResultStatus.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting asset {AssetId} for file channel", asset.Id);
            asset.Error = ex.Message;
            return IngestResultStatus.Failed;
        }
    }

    public async Task<IngestResultStatus> Ingest(AdjunctIngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var adjunct = ingestionContext.Adjunct;
        var isAnnotation = adjunct.IIIFLink == IIIFLinkType.Annotations;

        try
        {
            if (customerOriginStrategy.Optimised)
            {
                // The adjunct bytes remain in the (optimised) origin - Protagonist isn't storing them, so they
                // don't count towards the customer's stored-adjunct size. We still record the adjunct's size.
                long? contentSize;
                if (isAnnotation)
                {
                    var (annotationStatus, annotationSize) =
                        await ValidateAnnotationAtOrigin(ingestionContext, customerOriginStrategy, cancellationToken);
                    if (annotationStatus != IngestResultStatus.Success) return annotationStatus;
                    contentSize = annotationSize;
                }
                else
                {
                    logger.LogDebug(
                        "Adjunct {AdjunctId} for Asset {Asset} is at optimised origin, no 'file' handling required - recording size only",
                        adjunct.Id, ingestionContext.AssetId);
                    contentSize = await GetOptimisedAdjunctSize(ingestionContext, cancellationToken);
                }

                RecordAdjunctSizeChange(ingestionContext, contentSize, isOptimised: true);
                return IngestResultStatus.Success;
            }

            var targetStorageLocation = storageKeyGenerator.GetStoredAdjunctLocation(ingestionContext.AssetId, adjunct);

            var adjunctInBucket = await assetToS3.CopyOriginToStorage(targetStorageLocation,
                ingestionContext,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(ingestionContext.Asset.Customer),
                customerOriginStrategy,
                isAnnotation ? IsValidAnnotationJsonFile : null,
                cancellationToken);

            if (assetIngestorSizeCheck.DoesAssetFromOriginExceedAllowance(adjunctInBucket, adjunct))
            {
                return IngestResultStatus.StorageLimitExceeded;
            }

            ingestionContext.StoredObjects[targetStorageLocation] = adjunctInBucket.AssetSize;
            RecordAdjunctSizeChange(ingestionContext, adjunctInBucket.AssetSize, isOptimised: false);

            return IngestResultStatus.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting asset Adjunct {AdjunctId} for Asset {Asset} for file channel",
                adjunct.Id, ingestionContext.AssetId);
            adjunct.Error = ex.Message;
            return IngestResultStatus.Failed;
        }
    }
    
    private static void UpdateIngestionContext(IngestionContext ingestionContext, AssetFromOrigin itemInBucket,
        RegionalisedObjectInBucket targetStorageLocation)
    {
        // Asset ingestion. Adjuncts have their own size/storage handling (see RecordAdjunctSizeChange).
        ingestionContext.StoredObjects[targetStorageLocation] = itemInBucket.AssetSize;
        ingestionContext.WithStorage(assetSize: itemInBucket.AssetSize);
    }

    /// <summary>
    /// Records the ingested adjunct's content <see cref="Adjunct.Size"/> and computes the signed delta to apply to
    /// the customer's stored-adjunct size totals (<see cref="AdjunctIngestionContext.StoredSizeDelta"/>).
    /// </summary>
    /// <remarks>
    /// The delta is <c>newContribution - prevContribution</c>. Optimised adjuncts keep their bytes in the origin so
    /// contribute 0 regardless of size; this correctly decrements when a hosted adjunct moves to an optimised origin,
    /// and increments in the reverse direction. Must be called with the adjunct's <em>pre-ingest</em> DB values still
    /// in place (i.e. before this method overwrites them).
    /// </remarks>
    private void RecordAdjunctSizeChange(AdjunctIngestionContext context, long? newContentSize, bool isOptimised)
    {
        var adjunct = context.Adjunct;

        var prevContribution = adjunct.Optimised ? 0 : (adjunct.Size ?? 0);
        var newContribution = isOptimised ? 0 : (newContentSize ?? 0);

        context.WithStoredSizeDelta(newContribution - prevContribution);

        if (newContentSize.HasValue)
        {
            adjunct.Size = newContentSize.Value;
        }
        else if (isOptimised)
        {
            logger.LogWarning(
                "Unable to determine size for optimised adjunct {AdjunctId}, Asset {AssetId}; leaving size unchanged",
                adjunct.Id, context.AssetId);
        }

        adjunct.Optimised = isOptimised;
    }

    private async Task<(IngestResultStatus status, long? size)> ValidateAnnotationAtOrigin(
        AdjunctIngestionContext ingestionContext, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken)
    {
        var adjunct = ingestionContext.Adjunct;

        await using var originResponse =
            await originFetcher.LoadFromOrigin(adjunct, customerOriginStrategy, cancellationToken);

        if (originResponse.IsEmpty || originResponse.Stream.IsNull())
        {
            logger.LogError(
                "Unable to read annotation adjunct {AdjunctId} for Asset {AssetId} content from origin for validation",
                adjunct.Id, ingestionContext.AssetId);
            adjunct.Error = "Unable to read annotation content for validation";
            return (IngestResultStatus.Failed, null);
        }

        var validationError = await GetJsonValidationError(originResponse.Stream, cancellationToken);
        if (validationError != null)
        {
            logger.LogError(
                "Annotation adjunct {AdjunctId} for Asset {AssetId} content is not valid JSON",
                adjunct.Id, ingestionContext.AssetId);
            adjunct.Error = validationError;
            return (IngestResultStatus.Failed, null);
        }

        // We fetched the content to validate it, so we can capture its size at the same time
        return (IngestResultStatus.Success, originResponse.ContentLength);
    }

    /// <summary>
    /// Determines the size of an adjunct held at an optimised origin (where Protagonist doesn't fetch/copy the
    /// bytes) via an S3 HEAD request. Returns null if the size cannot be determined.
    /// </summary>
    private async Task<long?> GetOptimisedAdjunctSize(AdjunctIngestionContext ingestionContext,
        CancellationToken cancellationToken)
    {
        var adjunct = ingestionContext.Adjunct;

        var origin = RegionalisedObjectInBucket.Parse(adjunct.Origin ?? string.Empty);
        if (origin == null)
        {
            logger.LogWarning(
                "Unable to parse origin '{Origin}' for optimised adjunct {AdjunctId}, Asset {AssetId}; size not recorded",
                adjunct.Origin, adjunct.Id, ingestionContext.AssetId);
            return null;
        }

        var headers = await bucketReader.GetObjectHeaders(origin, cancellationToken: cancellationToken);
        return headers?.ContentLength;
    }

    private async Task<string?> IsValidAnnotationJsonFile(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = System.IO.File.OpenRead(filePath);
        return await GetJsonValidationError(stream, cancellationToken);
    }

    /// <summary>Returns null if <paramref name="stream"/> contains valid JSON, or an error message if not.</summary>
    private async Task<string?> GetJsonValidationError(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogTrace(ex, "Failed to parse JSON from stream");
            return AnnotationInvalidJsonError;
        }
    }

    private const string AnnotationInvalidJsonError = "Annotation content is not valid JSON";
}
