using Amazon.S3;
using Amazon.S3.Model;
using DLCS.AWS.S3.Models;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.S3
{
    public class S3BucketWriter : IBucketWriter
    {
        private readonly IAmazonS3 s3Client;
        private readonly ILogger<S3BucketWriter> logger;

        public S3BucketWriter(IAmazonS3 s3Client, ILogger<S3BucketWriter> logger)
        {
            this.s3Client = s3Client;
            this.logger = logger;
        }

        public async Task CopyWithinBucket(string bucket, string sourceKey, string destKey)
        {
            logger.LogDebug("Copying {Source} to {Destination} in {Bucket}", sourceKey, destKey, bucket);
            try
            {
                var request = new CopyObjectRequest
                {
                    SourceBucket = bucket,
                    SourceKey = sourceKey,
                    DestinationBucket = bucket,
                    DestinationKey = destKey
                };
                CopyObjectResponse response = await s3Client.CopyObjectAsync(request);
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
                CopyObjectResponse response = await s3Client.CopyObjectAsync(request);
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

        public async Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
            CancellationToken cancellationToken = default)
        {
            // 1. Put object-specify only key name for the new object.
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

            PutObjectResponse? response = await WriteToBucketInternal(putRequest);
            return response != null;
        }
        
        public async Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string? contentType = null)
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = dest.Bucket,
                Key = dest.Key,
                FilePath = filePath,
            };

            if (!string.IsNullOrEmpty(contentType)) putRequest.ContentType = contentType;
            
            PutObjectResponse? response = await WriteToBucketInternal(putRequest);
            return response != null;
        }

        public async Task DeleteFromBucket(params ObjectInBucket[] toDelete)
        {
            try
            {
                var deleteObjectsRequest = new DeleteObjectsRequest
                {
                    BucketName = toDelete[0].Bucket,
                    Objects = toDelete.Select(oib => new KeyVersion{Key = oib.Key}).ToList(),
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
    }
}