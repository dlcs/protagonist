﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Exceptions;
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

        public async Task<Stream?> GetObjectFromBucket(ObjectInBucket objectInBucket)
        {
            var getObjectRequest = objectInBucket.AsGetObjectRequest();
            try
            {
                var getResponse = await s3Client.GetObjectAsync(getObjectRequest);
                return getResponse.ResponseStream;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation(e, "Could not find S3 object '{S3ObjectRequest}'", getObjectRequest);
                return null;
            }
            catch (AmazonS3Exception e)
            {
                logger.LogWarning(e, "Could not copy S3 Stream for {S3ObjectRequest}; {StatusCode}",
                    getObjectRequest, e.StatusCode);
                throw new HttpException(e.StatusCode, $"Error copying S3 stream for {getObjectRequest}", e);
            }
        }

        public async Task<string[]> GetMatchingKeys(ObjectInBucket rootKey)
        {
            var listObjectsRequest = rootKey.AsListObjectsRequest();
            try
            {
                var response = await s3Client.ListObjectsAsync(listObjectsRequest, CancellationToken.None);
                return response.S3Objects.Select(obj => obj.Key).OrderBy(s => s).ToArray();
            }
            catch (AmazonS3Exception e)
            {
                logger.LogWarning(e, "Error getting matching keys {S3ListObjectRequest}; {StatusCode}",
                    listObjectsRequest, e.StatusCode);
                throw new HttpException(e.StatusCode, $"Error getting S3 objects for {listObjectsRequest}", e);
            }
        }

        public async Task CopyWithinBucket(string bucket, string sourceKey, string destKey)
        {
            logger.LogDebug("Copying {Source} to {Destination} in {Bucket}", sourceKey, destKey, bucket);
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
                logger.LogWarning(e, "Error encountered on server. Message:'{Message}' when writing an object", e.Message);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Unknown encountered on server. Message:'{Message}' when writing an object", e.Message);
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
                logger.LogWarning(e, "S3 Error encountered. Message:'{Message}' when writing an object", e.Message);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Unknown encountered on server. Message:'{Message}' when writing an object", e.Message);
            }
        }
    }
}
