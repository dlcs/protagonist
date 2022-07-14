using DLCS.Core.Strings;

namespace DLCS.AWS.S3.Models
{
    public static class ObjectInBucketX
    {
        /// <summary>
        /// Get the full s3:// uri for object in bucket, including region if found
        /// </summary>
        /// <param name="objectInBucket"><see cref="ObjectInBucket"/> to get s3 uri for</param>
        /// <returns></returns>
        /// <remarks>S3 URIs don't include the Region</remarks>
        public static Uri GetS3Uri(this ObjectInBucket objectInBucket)
            => new($"s3://{objectInBucket.Bucket}/{objectInBucket.Key}");

        /// <summary>
        /// Get the HTTPS URI for <see cref="ObjectInBucket"/>
        /// </summary>
        /// <param name="objectInBucket"><see cref="ObjectInBucket"/> to get https uri for</param>
        /// <returns></returns>
        /// <remarks>See https://docs.aws.amazon.com/AmazonS3/latest/userguide/access-bucket-intro.html for more info</remarks>
        public static Uri GetHttpUri(this ObjectInBucket objectInBucket)
        {
            // Return Virtual-hosted-style access URL for S3 bucket 
            if (objectInBucket is RegionalisedObjectInBucket regionalised && regionalised.Region.HasText())
            {
                return new Uri(
                    $"https://{regionalised.Bucket}.s3.{regionalised.Region}.amazonaws.com/{regionalised.Key}");
            }
            
            // NOTE - this uses legacy global endpoint and assumes us-east-1 so best avoided
            return new Uri($"https://s3.amazonaws.com/{objectInBucket.Bucket}/{objectInBucket.Key}");
        }
    }
}