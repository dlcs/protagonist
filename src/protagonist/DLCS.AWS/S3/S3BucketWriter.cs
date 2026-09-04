using System.Diagnostics;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.S3;

public class S3BucketWriter(
    IAwsClientProvider<IAmazonS3> s3ClientProvider,
    IOptions<AWSSettings> awsOptions,
    ILogger<S3BucketWriter> logger)
    : IBucketWriter
{
    private readonly S3Settings s3Settings = awsOptions.Value.S3;

    private IAmazonS3 S3Client => s3ClientProvider.GetClient();

    public async Task CopyObject(ObjectInBucket source, ObjectInBucket destination)
    {
        logger.LogDebug("Copying {Source} to {Destination}", source, destination);
        try
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = source.Bucket,
                SourceKey = source.Key,
                DestinationBucket = destination.Bucket,
                DestinationKey = destination.Key
            };
            var response = await S3Client.CopyObjectAsync(request);
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "Error encountered on server. Message:'{Message}' when writing an object",
                e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unknown encountered on server. Message:'{Message}' when writing an object",
                e.Message);
        }
    }

    /// <summary>
    /// Copy a large file between buckets using multi part upload.
    /// This should always be used for files >5GiB
    /// </summary>
    /// <param name="source">Bucket where object is currently stored.</param>
    /// <param name="destination">Target bucket where object is to be stored.</param>
    /// <param name="verifySize">Function to verify objectSize prior to copying. Not copied if false returned.</param>
    /// <param name="contentType">ContentType to set on uploaded object</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>ResultStatus signifying success or failure alongside ContentSize</returns>
    /// <remarks>See https://docs.aws.amazon.com/AmazonS3/latest/dev/CopyingObjctsUsingLLNetMPUapi.html </remarks>
    public async Task<LargeObjectCopyResult> CopyLargeObject(ObjectInBucket source, ObjectInBucket destination,
        Func<long, Task<bool>>? verifySize = null, string? contentType = null, CancellationToken token = default)
    {
        long? objectSize = null;
        string? uploadId = null;
        var success = false;
        var timer = Stopwatch.StartNew();

        try
        {
            var sourceMetadata = await GetObjectMetadata(source, token);
            if (sourceMetadata == null || sourceMetadata.ContentLength == 0)
            {
                var notFoundResponse = new LargeObjectCopyResult(LargeObjectStatus.SourceNotFound);
                var destinationMetadata = await GetObjectMetadata(destination, token);
                notFoundResponse.DestinationExists = destinationMetadata != null;
                return notFoundResponse;
            }

            objectSize = sourceMetadata.ContentLength;
            var notNullObjectSize = objectSize.Value;

            if (verifySize != null)
            {
                if (!await verifySize.Invoke(notNullObjectSize))
                {
                    logger.LogInformation("Aborting multipart upload for {Target} as size verification failed",
                        destination);
                    return new LargeObjectCopyResult(LargeObjectStatus.FileTooLarge, notNullObjectSize);
                }
            }

            var partSize = GetPartSize(notNullObjectSize);
            var numberOfParts = (int)Math.Ceiling((double)notNullObjectSize / partSize);

            uploadId = await InitiateMultipartUpload(destination, contentType);
            logger.LogDebug("Starting copying {UploadId} in {Parts} parts of size {PartSize}", uploadId, numberOfParts,
                partSize);

            // Build all part requests
            var partRequests =
                GetCopyPartRequests(source, destination, numberOfParts, notNullObjectSize, uploadId, partSize);

            // Copy parts in parallel with bounded concurrency
            var copyResponses = new CopyPartResponse[numberOfParts];
            await Parallel.ForEachAsync(
                partRequests,
                new ParallelOptions
                    { MaxDegreeOfParallelism = s3Settings.CopyPartConcurrency, CancellationToken = token },
                async (request, ct) =>
                {
                    copyResponses[request.PartNumber!.Value - 1] = await S3Client.CopyPartAsync(request, ct);
                });

            var completeRequest = new CompleteMultipartUploadRequest
            {
                Key = destination.Key,
                BucketName = destination.Bucket,
                UploadId = uploadId,
            };
            completeRequest.AddPartETags(copyResponses);
            await S3Client.CompleteMultipartUploadAsync(completeRequest, token);
            success = true;
            return new LargeObjectCopyResult(LargeObjectStatus.Success, objectSize);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Cancellation requested, aborting multipart upload for {Target}", destination);
            if (uploadId != null)
            {
                await S3Client.AbortMultipartUploadAsync(destination.Bucket, destination.Key, uploadId,
                    CancellationToken.None);
            }
            return new LargeObjectCopyResult(LargeObjectStatus.Cancelled, objectSize);
        }
        catch (AmazonS3Exception e)
        {
            logger.LogError(e,
                "S3 Error encountered copying bucket-bucket item. From '{Source}' to '{Destination}'",
                source, destination);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Error during multipart bucket-bucket copy. From '{Source}' to '{Destination}'", source, destination);
        }
        finally
        {
            timer.Stop();
            if (success)
            {
                logger.LogInformation("Copied large file to '{Target}' in {Elapsed}ms", destination,
                    timer.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation("Failed to copy large file to '{Target}'. Failed after {Elapsed}ms", destination,
                    timer.ElapsedMilliseconds);
            }
        }
        
        return new LargeObjectCopyResult(LargeObjectStatus.Error, objectSize);
    }

    private static List<CopyPartRequest> GetCopyPartRequests(ObjectInBucket source, ObjectInBucket destination,
        int numberOfParts, long objectSize, string uploadId, long partSize)
    {
        var partRequests = new List<CopyPartRequest>(numberOfParts);
        long bytePosition = 0;
        for (var i = 1; bytePosition < objectSize; i++)
        {
            partRequests.Add(new CopyPartRequest
            {
                DestinationBucket = destination.Bucket,
                DestinationKey = destination.Key,
                SourceBucket = source.Bucket,
                SourceKey = source.Key,
                UploadId = uploadId,
                FirstByte = bytePosition,
                LastByte = bytePosition + partSize - 1 >= objectSize
                    ? objectSize - 1
                    : bytePosition + partSize - 1,
                PartNumber = i
            });
            bytePosition += partSize;
        }

        return partRequests;
    }

    private static long GetPartSize(long objectSize)
    {
        // 16 MB matches TransferUtility's default and reduces API call overhead vs the 5 MB S3 minimum
        const long minPartSize = 16 * 1024 * 1024;
        const double maxParts = 10000;
        var partSize = Math.Max(minPartSize, (long)Math.Ceiling(objectSize / maxParts));
        return partSize;
    }

    public async Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = dest.Bucket,
            Key = dest.Key,
            ContentBody = content,
            ContentType = contentType
        };

        PutObjectResponse? response = await WriteToBucketInternal(putRequest, cancellationToken);
    }

    public async Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string? contentType = null)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = dest.Bucket,
            Key = dest.Key,
            InputStream = content,
        };

        if (!string.IsNullOrEmpty(contentType)) putRequest.ContentType = contentType;

        var response = await WriteToBucketInternal(putRequest);
        return response != null;
    }

    public async Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string? contentType = null,
        CancellationToken token = default)
    {
        try
        {
            // Transfer utility uses multi-part upload internally if the file is large enough to warrant it (>16MB)
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = dest.Bucket,
                Key = dest.Key,
                FilePath = filePath,
            };

            if (!string.IsNullOrEmpty(contentType)) uploadRequest.ContentType = contentType;
                
            using var transferUtil = new TransferUtility(S3Client);
            await transferUtil.UploadAsync(uploadRequest, token);
            return true;
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "S3 Error encountered writing file to bucket. Key: '{S3Key}'", dest);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unknown error encountered writing file to bucket. Key: '{S3Key}'", dest);
        }

        return false;
    }

    public async Task DeleteFromBucket(params ObjectInBucket[] toDelete)
    {
        try
        {
            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                BucketName = toDelete[0].Bucket,
                Objects = toDelete.Select(oib => new KeyVersion { Key = oib.Key }).ToList(),
            };

            await S3Client.DeleteObjectsAsync(deleteObjectsRequest);
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "S3 Error encountered. Message:'{Message}' when deleting objects from bucket",
                e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e,
                "Unknown encountered on server. Message:'{Message}' when deleting objects from bucket", e.Message);
        }
    }

    public async Task DeleteFolder(ObjectInBucket root, bool deleteRoot)
    {
        // NOTE - this is based on the S3DirectoryInfo.Delete method that was removed from SDK
        try
        {
            var listObjectsRequest = new ListObjectsRequest
            {
                BucketName = root.Bucket,
                Prefix = root.Key
            };

            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                BucketName = root.Bucket
            };

            if (deleteRoot && root.Key != null) deleteObjectsRequest.AddKey(root.Key.TrimEnd('/'));

            ListObjectsResponse listObjectsResponse;
            do
            {
                listObjectsResponse = await S3Client.ListObjectsAsync(listObjectsRequest);
                foreach (var item in (listObjectsResponse.S3Objects ?? []).OrderBy(x => x.Key))
                {
                    deleteObjectsRequest.AddKey(item.Key);
                    if (deleteObjectsRequest.Objects.Count == 1000)
                    {
                        await S3Client.DeleteObjectsAsync(deleteObjectsRequest);
                        deleteObjectsRequest.Objects.Clear();
                    }

                    listObjectsRequest.Marker = item.Key;
                }
            } while (listObjectsResponse.IsTruncated ?? false);
            
            if (deleteObjectsRequest.Objects is { Count: > 0 })
            {
                await S3Client.DeleteObjectsAsync(deleteObjectsRequest);
            }
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning("S3 Error encountered. Message:'{Message}' when deleting folder '{Folder}' from bucket",
                e.Message, root);
        }
        catch (Exception e)
        {
            logger.LogWarning(e,
                "Unknown encountered on server. Message:'{Message}' when deleting folder '{Folder}' from bucket",
                e.Message, root);
        }
    }

    private async Task<PutObjectResponse?> WriteToBucketInternal(PutObjectRequest putRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PutObjectResponse response = await S3Client.PutObjectAsync(putRequest, cancellationToken);
            return response;
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "S3 Error encountered. Message:'{Message}' when writing an object", e.Message);
            return null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unknown encountered on server. Message:'{Message}' when writing an object",
                e.Message);
            return null;
        }
    }

    private async Task<string> InitiateMultipartUpload(ObjectInBucket destination, string? contentType)
    {
        var request = new InitiateMultipartUploadRequest
            { BucketName = destination.Bucket, Key = destination.Key, ContentType = contentType };

        var response = await S3Client.InitiateMultipartUploadAsync(request);
        return response.UploadId;
    }

    private async Task<GetObjectMetadataResponse?> GetObjectMetadata(ObjectInBucket resource,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = resource.AsObjectMetadataRequest();
            return await S3Client.GetObjectMetadataAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
