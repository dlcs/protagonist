using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Model.Storage;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Storage.S3
{
    public class BucketReader : IBucketReader
    {
        private readonly IAmazonS3 s3Client;
        private readonly ILogger<BucketReader> logger;

        public BucketReader(IAmazonS3 s3Client, ILogger<BucketReader> logger)
        {
            this.s3Client = s3Client;
            this.logger = logger;
        }

        public async Task WriteObjectFromBucket(ObjectInBucket objectInBucket, Stream targetStream)
        {
            var getObjectRequest = objectInBucket.AsGetObjectRequest();
            try
            {
                var getResponse = await s3Client.GetObjectAsync(getObjectRequest);
                await getResponse.ResponseStream.CopyToAsync(targetStream);
            }
            catch (AmazonS3Exception e)
            {
                logger.LogWarning(e, $"Could not copy S3 Stream for {getObjectRequest}; {e.StatusCode}");
                throw;

                // TODO convert this into an application (not AWS) exception that still conveys status codes
                // so callers can do different things for 404 etc.

                // TODO - we almost certainly don't want to write this here, bubble up to caller
                // and let caller write/handle the error better (e.g., set status code)
                //var writer = new StreamWriter(targetStream);
                //writer.Write(e.Message);
            }
        }

        public async Task<string[]> GetMatchingKeys(ObjectInBucket rootKey)
        {
            var request = new ListObjectsRequest
            {
                BucketName = rootKey.Bucket,
                Prefix = rootKey.Key
            };
            var response = await s3Client.ListObjectsAsync(request, CancellationToken.None);
            return response.S3Objects.Select(obj => obj.Key).OrderBy(s => s).ToArray();
        }

        public async Task CopyWithinBucket(string bucket, string sourceKey, string destKey)
        {
            logger.LogInformation($"Copy {sourceKey} to {destKey} in {bucket}");
            try
            {
                CopyObjectRequest request = new CopyObjectRequest
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
                logger.LogWarning(e, "Error encountered on server. Message:'{0}' when writing an object", e.Message);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
            }
        }

        public async Task WriteToBucket(ObjectInBucket dest, string content, string contentType)
        {
            try
            {
                // 1. Put object-specify only key name for the new object.
                var putRequest = new PutObjectRequest
                {
                    BucketName = dest.Bucket,
                    Key = dest.Key,
                    ContentBody = content,
                    ContentType = contentType
                };

                PutObjectResponse response = await s3Client.PutObjectAsync(putRequest);
            }
            catch (AmazonS3Exception e)
            {
                logger.LogWarning(e, "Error encountered ***. Message:'{0}' when writing an object", e.Message);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
            }
        }
    }
}
