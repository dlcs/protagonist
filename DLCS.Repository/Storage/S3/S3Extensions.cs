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
    }
}
