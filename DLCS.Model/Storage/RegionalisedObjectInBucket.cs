using System.Text.RegularExpressions;
using DLCS.Core.Guard;

namespace DLCS.Model.Storage
{
    public class RegionalisedObjectInBucket : ObjectInBucket
    {
        public string Region { get; set; }
        
        public RegionalisedObjectInBucket(string bucket, string key = null, string region = null) : base(bucket, key)
        {
            Region = region;
        }
        
        /// <summary>
        /// Get fully qualified S3 uri (e.g. s3://eu-west-1/bucket/key)
        /// </summary>
        public string GetS3QualifiedUri() => $"s3://{Region.ThrowIfNullOrWhiteSpace(nameof(Region))}/{Bucket}/{Key}";
        
        // TODO - naive implementation, won't work for us-east-1
        /// <summary>
        /// Get http uri for S3 object (e.g. https://s3.eu-west-1/bucket/key)
        /// </summary>
        public string GetHttpUri() => $"https://{Bucket}.s3-{Region.ThrowIfNullOrWhiteSpace(nameof(Region))}.amazonaws.com/{Key}";
        
        // NOTE(DG) Regex's and logic moved from deliverator
        private static readonly Regex RegexS3Qualified = new(@"s3\:\/\/(.*?)\/(.*?)\/(.*)", RegexOptions.Compiled);
        
        // us-east-1 uses period instead of dash
        private static readonly Regex RegexHttp1 = new(@"^http[s]?\:\/\/s3[\-\.](\S+\-\S+\-\d)\.amazonaws\.com\/(.*?)\/(.*)$", RegexOptions.Compiled); 
        
        // no region for this one
        private static readonly Regex RegexHttp2 = new(@"^http[s]?:\/\/(.*?)\.s3\.amazonaws\.com\/(.*)$", RegexOptions.Compiled); 
        
        // includes region
        private static readonly Regex RegexHttp3 = new(@"^http[s]?:\/\/(.*?)\.s3\.(.*?)\.amazonaws\.com\/(.*)$", RegexOptions.Compiled); 
        
        // bucket name in path (assumes same region as caller)
        private static readonly Regex RegexHttp4 = new(@"^http[s]?:\/\/s3\.amazonaws\.com\/(.*?)\/(.*)$", RegexOptions.Compiled); 

        public static RegionalisedObjectInBucket Parse(string uri)
        {
            if (TryParseBucketInfo(RegexS3Qualified, uri, out var result, qualified: true))
            {
                return result;
            }

            if (TryParseBucketInfo(RegexHttp1, uri, out result, qualified: true))
            {
                return result;
            }

            if (TryParseBucketInfo(RegexHttp2, uri, out result, qualified: false))
            {
                return result;
            }

            if (TryParseBucketInfo(RegexHttp3, uri, out result, qualified: true, following: true))
            {
                return result;
            }

            if (TryParseBucketInfo(RegexHttp4, uri, out result, qualified: false))
            {
                return result;
            }

            return null;
        }

        private static bool TryParseBucketInfo(Regex regex, string s, out RegionalisedObjectInBucket bucket,
            bool qualified = false, bool following = false)
        {
            var match = regex.Match(s);

            if (!match.Success)
            {
                bucket = null;
                return false;
            }

            if (qualified)
            {
                if (following)
                {
                    bucket = new RegionalisedObjectInBucket(
                        match.Groups[1].Value,
                        match.Groups[3].Value,
                        match.Groups[2].Value);
                    return true;
                }

                bucket = new RegionalisedObjectInBucket(
                    match.Groups[2].Value,
                    match.Groups[3].Value,
                    match.Groups[1].Value);
                return true;
            }

            bucket = new RegionalisedObjectInBucket(
                match.Groups[1].Value,
                match.Groups[2].Value);
            return true;
        }
    }
}