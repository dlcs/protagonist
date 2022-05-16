using Amazon.S3.Model;
using DLCS.AWS.S3.Models;

namespace DLCS.AWS.S3
{
    public static class S3Extensions
    {
        /// <summary>
        /// Convert <see cref="ObjectInBucket"/> to <see cref="GetObjectRequest"/>
        /// </summary>
        public static GetObjectRequest AsGetObjectRequest(this ObjectInBucket resource) =>
            new()
            {
                BucketName = resource.Bucket,
                Key = resource.Key
            };

        /// <summary>
        /// Convert <see cref="ObjectInBucket"/> to <see cref="ListObjectsRequest"/>
        /// </summary>
        public static ListObjectsRequest AsListObjectsRequest(this ObjectInBucket resource) =>
            new()
            {
                BucketName = resource.Bucket,
                Prefix = resource.Key
            };

        /// <summary>
        /// Get "{bucket}/{key}" from <see cref="GetObjectRequest"/>.
        /// </summary>
        public static string AsBucketAndKey(this GetObjectRequest getObjectRequest) =>
            $"{getObjectRequest.BucketName}/{getObjectRequest.Key}";
        
        /// <summary>
        /// Convert <see cref="GetObjectResponse"/> to <see cref="ObjectFromBucket"/>
        /// </summary>
        public static ObjectFromBucket AsObjectInBucket(this GetObjectResponse getObjectResponse,
            ObjectInBucket objectInBucket)
            => new(
                objectInBucket,
                getObjectResponse.ResponseStream,
                getObjectResponse.Headers.AsObjectInBucketHeaders()
            );

        /// <summary>
        /// Convert <see cref="ObjectInBucket"/> to <see cref="GetObjectMetadataRequest"/>
        /// </summary>
        public static GetObjectMetadataRequest AsObjectMetadataRequest(this ObjectInBucket resource)
            => new()
            {
                BucketName = resource.Bucket,
                Key = resource.Key,
            };

        public static ObjectInBucketHeaders AsObjectInBucketHeaders(this HeadersCollection headersCollection)
            => new()
            {
                CacheControl = headersCollection.CacheControl,
                ContentDisposition = headersCollection.ContentDisposition,
                ContentEncoding = headersCollection.ContentEncoding,
                ContentLength = headersCollection.ContentLength == -1L ? null : headersCollection.ContentLength,
                ContentMD5 = headersCollection.ContentMD5,
                ContentType = headersCollection.ContentType,
                ExpiresUtc = headersCollection.ExpiresUtc
            };
    }
}
