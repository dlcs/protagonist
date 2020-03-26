using System.Reflection.Metadata.Ecma335;
using Amazon.S3.Model;
using DLCS.Model.Storage;

namespace DLCS.Repository.Storage.S3
{
    public static class S3Extensions
    {
        public static GetObjectRequest AsGetObjectRequest(this ObjectInBucket resource) =>
            new GetObjectRequest
            {
                BucketName = resource.Bucket,
                Key = resource.Key
            };

        public static ListObjectsRequest AsListObjectsRequest(this ObjectInBucket resource) =>
            new ListObjectsRequest
            {
                BucketName = resource.Bucket,
                Prefix = resource.Key
            };

        public static string AsBucketAndKey(this GetObjectRequest getObjectRequest) =>
            $"{getObjectRequest.BucketName}/{getObjectRequest.Key}";
    }
}
