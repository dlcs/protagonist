using System.Text.RegularExpressions;

namespace DLCS.AWS.S3.Models;

/// <summary>
/// Represents a bucket that may have an explicit region set
/// </summary>
/// <remarks>See https://docs.aws.amazon.com/AmazonS3/latest/userguide/access-bucket-intro.html</remarks>
public class RegionalisedObjectInBucket : ObjectInBucket
{
    public string? Region { get; set; }
    
    public RegionalisedObjectInBucket(string bucket, string? key = null, string? region = null) : base(bucket, key)
    {
        Region = region;
    }

    // s3:// URI with region included. This is not an official format for S3 but is used + required by Deliverator
    // Leaving this here until we have retired that version for backwards compat
    // region is in format eu-west-2, ap-southeast-1, us-east-1
    private static readonly Regex RegexS3Qualified = new(@"s3\:\/\/(\w{2}-\w+\-\d)\/(.*?)\/(.*)", RegexOptions.Compiled);
    
    private static readonly Regex RegexS3 = new(@"s3\:\/\/(.*?)\/(.*)", RegexOptions.Compiled);
    
    // us-east-1 uses period instead of dash
    private static readonly Regex RegexHttp1 = new(@"^http[s]?\:\/\/s3[\-\.](\S+\-\S+\-\d)\.amazonaws\.com\/(.*?)\/(.*)$", RegexOptions.Compiled); 
    
    // no region for this one
    private static readonly Regex RegexHttp2 = new(@"^http[s]?:\/\/(.*?)\.s3\.amazonaws\.com\/(.*)$", RegexOptions.Compiled); 
    
    // includes region
    private static readonly Regex RegexHttp3 = new(@"^http[s]?:\/\/(.*?)\.s3\.(.*?)\.amazonaws\.com\/(.*)$", RegexOptions.Compiled); 
    
    // bucket name in path (assumes same region as caller)
    private static readonly Regex RegexHttp4 = new(@"^http[s]?:\/\/s3\.amazonaws\.com\/(.*?)\/(.*)$", RegexOptions.Compiled); 

    /// <summary>
    /// Parse the provided Uri to a <see cref="RegionalisedObjectInBucket"/>. The uri can be in http:// or s3:// format.
    /// The result may or may not have a region value. 
    /// </summary>
    /// <param name="uri">Uri to parse</param>
    /// <param name="throwIfUnableToParse"> If true, an exception is raised if the uri cannot be parsed</param>
    /// <returns>Parsed object, or null of unable to parse</returns>
    /// <exception cref="FormatException">Thrown if throwIfUnableToParse=true and unable to parse</exception>
    public static RegionalisedObjectInBucket? Parse(string uri, bool throwIfUnableToParse = false)
    {
        if (TryParseBucketInfo(RegexS3Qualified, uri, out var result, qualified: true))
        {
            return result;
        }

        if (TryParseBucketInfo(RegexS3, uri, out result, qualified: false))
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

        if (throwIfUnableToParse)
        {
            throw new FormatException($"Unable to parse {uri} to an ObjectInBucket");
        }
        return null;
    }

    /// <summary>
    /// Attempt to match string to regex
    /// </summary>
    /// <param name="regex">Regex to match</param>
    /// <param name="s">Input string</param>
    /// <param name="bucket">Output bucket object</param>
    /// <param name="qualified">Contains region</param>
    /// <param name="following">Region at end</param>
    /// <remarks>This is migrated from deliverator</remarks>
    private static bool TryParseBucketInfo(Regex regex, string s, out RegionalisedObjectInBucket? bucket,
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
    
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((RegionalisedObjectInBucket)obj);
    }
    
    public override int GetHashCode() => HashCode.Combine(Bucket, Key);
    
    public static bool operator ==(RegionalisedObjectInBucket? objectInBucket1, RegionalisedObjectInBucket? objectInBucket2)
    {
        if (objectInBucket1 is null)
        {
            return objectInBucket2 is null;
        }
        
        if (objectInBucket2 is null)
        {
            return false;
        }
        
        return objectInBucket1.Equals(objectInBucket2);
    }

    public static bool operator !=(RegionalisedObjectInBucket? objectInBucket1, RegionalisedObjectInBucket? objectInBucket2) 
        => !(objectInBucket1 == objectInBucket2);

    private bool Equals(RegionalisedObjectInBucket other)
        => Region == other.Region && base.Equals(other);
}
