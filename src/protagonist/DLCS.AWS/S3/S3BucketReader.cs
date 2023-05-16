using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.AWS.S3.Models;
using DLCS.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.S3;

public class S3BucketReader : IBucketReader
{
    private readonly IAmazonS3 s3Client;
    private readonly ILogger<S3BucketReader> logger;

    public S3BucketReader(IAmazonS3 s3Client, ILogger<S3BucketReader> logger)
    {
        this.s3Client = s3Client;
        this.logger = logger;
    }
    
    public async Task<Stream?> GetObjectContentFromBucket(ObjectInBucket objectInBucket,
        CancellationToken cancellationToken = default)
    {
        var getObjectRequest = objectInBucket.AsGetObjectRequest();
        try
        {
            GetObjectResponse getResponse = await s3Client.GetObjectAsync(getObjectRequest, cancellationToken);
            return getResponse.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("Could not find S3 object '{S3ObjectRequest}'", getObjectRequest.AsBucketAndKey());
            return Stream.Null;
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "Could not copy S3 Stream for {S3ObjectRequest}; {StatusCode}",
                getObjectRequest.AsBucketAndKey(), e.StatusCode);
            throw new HttpException(e.StatusCode, $"Error copying S3 stream for {getObjectRequest.AsBucketAndKey()}", e);
        }
    }

    public async Task<ObjectFromBucket> GetObjectFromBucket(ObjectInBucket objectInBucket,
        CancellationToken cancellationToken = default)
    {
        var getObjectRequest = objectInBucket.AsGetObjectRequest();
        try
        {
            GetObjectResponse getResponse = await s3Client.GetObjectAsync(getObjectRequest, cancellationToken);
            return getResponse.AsObjectInBucket(objectInBucket);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("Could not find S3 object '{S3ObjectRequest}'", getObjectRequest.AsBucketAndKey());
            return new ObjectFromBucket(objectInBucket, null, null);
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "Could not copy S3 object for {S3ObjectRequest}; {StatusCode}",
                getObjectRequest.AsBucketAndKey(), e.StatusCode);
            throw new HttpException(e.StatusCode, $"Error copying S3 stream for {getObjectRequest.AsBucketAndKey()}", e);
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
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("Could not find S3 object '{S3ListObjectRequest}'", rootKey);
            throw new HttpException(e.StatusCode, $"Error getting S3 objects for {listObjectsRequest}", e);
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "Error getting matching keys {S3ListObjectRequest}; {StatusCode}",
                rootKey, e.StatusCode);
            throw new HttpException(e.StatusCode, $"Error getting S3 objects for {listObjectsRequest}", e);
        }
    }
}
