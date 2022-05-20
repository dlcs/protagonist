namespace DLCS.AWS.S3.Models
{
    public static class ObjectInBucketX
    {
        /// <summary>
        /// Get the full s3:// uri for object in bucket
        /// </summary>
        /// <param name="objectInBucket"><see cref="ObjectInBucket"/> to get s3 uri for</param>
        /// <returns></returns>
        public static string GetS3Uri(this ObjectInBucket objectInBucket)
            => $"s3://{objectInBucket.Bucket}/{objectInBucket.Key}";
    }
}