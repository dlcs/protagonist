using System.Diagnostics;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.S3;

public class S3BucketWriter : IBucketWriter
{
    private readonly IAmazonS3 s3Client;
    private readonly ILogger<S3BucketWriter> logger;

    public S3BucketWriter(IAmazonS3 s3Client, ILogger<S3BucketWriter> logger)
    {
        this.s3Client = s3Client;
        this.logger = logger;
    }

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
            var response = await s3Client.CopyObjectAsync(request);
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
    /// <param name="destIsPublic">If true the copied object is given public access rights</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>ResultStatus signifying success or failure alongside ContentSize</returns>
    /// <remarks>See https://docs.aws.amazon.com/AmazonS3/latest/dev/CopyingObjctsUsingLLNetMPUapi.html </remarks>
    public async Task<LargeObjectCopyResult> CopyLargeObject(ObjectInBucket source, ObjectInBucket destination,
        Func<long, Task<bool>>? verifySize = null, bool destIsPublic = false, CancellationToken token = default)
    {
        long? objectSize = null;
        var partSize = 5 * (long)Math.Pow(2, 20); // 5 MB
        var success = false;
        var timer = Stopwatch.StartNew();

        try
        {
            var sourceMetadata = await GetObjectMetadata(source, token);
            if (sourceMetadata == null)
            {
                var notFoundResponse = new LargeObjectCopyResult(LargeObjectStatus.SourceNotFound);
                var destinationMetadata = await GetObjectMetadata(destination, token);
                notFoundResponse.DestinationExists = destinationMetadata != null;
                return notFoundResponse;
            }
            
            objectSize = sourceMetadata.ContentLength;

            if (verifySize != null)
            {
                if (!await verifySize.Invoke(objectSize.Value))
                {
                    logger.LogInformation("Aborting multipart upload for {Target} as size verification failed",
                        destination);
                    return new LargeObjectCopyResult(LargeObjectStatus.FileTooLarge, objectSize);
                }
            }

            var numberOfParts = Convert.ToInt32(objectSize / partSize);
            var copyResponses = new List<CopyPartResponse>(numberOfParts);

            var uploadId = await InitiateMultipartUpload(destination, destIsPublic);

            long bytePosition = 0;
            for (int i = 1; bytePosition < objectSize; i++)
            {
                if (token.IsCancellationRequested)
                {
                    logger.LogInformation("Cancellation requested, aborting multipart upload for {Target}",
                        destination);
                    await s3Client.AbortMultipartUploadAsync(destination.Bucket, destination.Key, uploadId, token);
                    return new LargeObjectCopyResult(LargeObjectStatus.Cancelled, objectSize);
                }
                
                var copyRequest = new CopyPartRequest
                {
                    DestinationBucket = destination.Bucket,
                    DestinationKey = destination.Key,
                    SourceBucket = source.Bucket,
                    SourceKey = source.Key,
                    UploadId = uploadId,
                    FirstByte = bytePosition,
                    LastByte = bytePosition + partSize - 1 >= objectSize.Value
                        ? objectSize.Value - 1
                        : bytePosition + partSize - 1,
                    PartNumber = i
                };
                
                copyResponses.Add(await s3Client.CopyPartAsync(copyRequest, token));
                bytePosition += partSize;
            }

            // Complete the request
            var completeRequest = new CompleteMultipartUploadRequest
            {
                Key = destination.Key,
                BucketName = destination.Bucket,
                UploadId = uploadId,
            };
            completeRequest.AddPartETags(copyResponses);
            await s3Client.CompleteMultipartUploadAsync(completeRequest, token);
            success = true;
            return new LargeObjectCopyResult(LargeObjectStatus.Success, objectSize);
        }
        catch (OverflowException e)
        {
            logger.LogError(e,
                "Error getting number of parts to copy. From '{Source}' to '{Destination}'. Size {Size}", source,
                destination, objectSize);
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
                
            using var transferUtil = new TransferUtility(s3Client);
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

            await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
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

    private async Task<PutObjectResponse?> WriteToBucketInternal(PutObjectRequest putRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PutObjectResponse response = await s3Client.PutObjectAsync(putRequest, cancellationToken);
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
    
    private async Task<string> InitiateMultipartUpload(ObjectInBucket destination, bool makeTargetPublic)
    {
        var request = new InitiateMultipartUploadRequest { BucketName = destination.Bucket, Key = destination.Key };

        if (makeTargetPublic)
        {
            logger.LogInformation("Object {TargetObject} will have PublicRead ACL", destination);
            request.CannedACL = S3CannedACL.PublicRead;
        }
            
        var response = await s3Client.InitiateMultipartUploadAsync(request);
        return response.UploadId;
    }

    private async Task<GetObjectMetadataResponse?> GetObjectMetadata(ObjectInBucket resource,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = resource.AsObjectMetadataRequest();
            return await s3Client.GetObjectMetadataAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}