using DLCS.AWS.S3.Models;
 
namespace DLCS.AWS.Tests.S3.Models;

public class ObjectInBucketXTests
{
    [Fact]
    public void GetS3Uri_NoKey_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/");
    }
    
    [Fact]
    public void GetS3Uri_Key_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/key/for/item");
    }
    
    [Fact]
    public void GetS3Uri_Regionalised_NoRegion_NoKey_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/");
    }
    
    [Fact]
    public void GetS3Uri_Regionalised_NoRegion_Key_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/key/for/item");
    }
    
    [Fact]
    public void GetS3Uri_Regionalised_Region_NoKey_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", region: "eu-west-1");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/");
    }
    
    [Fact]
    public void GetS3Uri_Regionalised_Region_Key_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", "key/for/item", "eu-west-1");

        var s3Uri = objectInBucket.GetS3Uri();

        s3Uri.ToString().Should().Be("s3://my-bucket/key/for/item");
    }

    [Fact]
    public void GetLegacyS3Uri_NoKey_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetLegacyS3Uri("us-east-1");

        s3Uri.ToString().Should().Be("s3://us-east-1/my-bucket/");
    }
    
    [Fact]
    public void GetLegacyS3Uri_Key_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetLegacyS3Uri("us-east-1");

        s3Uri.ToString().Should().Be("s3://us-east-1/my-bucket/key/for/item");
    }
    
    [Fact]
    public void GetLegacyS3Uri_Regionalised_NoRegion_NoKey_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetLegacyS3Uri("us-east-1");

        s3Uri.ToString().Should().Be("s3://us-east-1/my-bucket/");
    }
    
    [Fact]
    public void GetLegacyS3Uri_Regionalised_NoRegion_Key_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetLegacyS3Uri("us-east-1");

        s3Uri.ToString().Should().Be("s3://us-east-1/my-bucket/key/for/item");
    }
    
    [Fact]
    public void GetLegacyS3Uri_Regionalised_Region_NoKey_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", region: "eu-west-1");

        var s3Uri = objectInBucket.GetLegacyS3Uri("us-east-1");

        s3Uri.ToString().Should().Be("s3://eu-west-1/my-bucket/");
    }
    
    [Fact]
    public void GetLegacyS3Uri_Regionalised_Region_Key_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", "key/for/item", "eu-west-1");

        var s3Uri = objectInBucket.GetLegacyS3Uri("us-east-1");

        s3Uri.ToString().Should().Be("s3://eu-west-1/my-bucket/key/for/item");
    }

    [Fact]
    public void GetHttpUri_NoKey_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetHttpUri();

        s3Uri.ToString().Should().Be("https://s3.amazonaws.com/my-bucket/");
    }
    
    [Fact]
    public void GetHttpUri_Key_Correct()
    {
        var objectInBucket = new ObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetHttpUri();

        s3Uri.ToString().Should().Be("https://s3.amazonaws.com/my-bucket/key/for/item");
    }
    
    [Fact]
    public void GetHttpUri_Regionalised_NoRegion_NoKey_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket");

        var s3Uri = objectInBucket.GetHttpUri();

        s3Uri.ToString().Should().Be("https://s3.amazonaws.com/my-bucket/");
    }
    
    [Fact]
    public void GetHttpUri_Regionalised_NoRegion_Key_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", "key/for/item");

        var s3Uri = objectInBucket.GetHttpUri();

        s3Uri.ToString().Should().Be("https://s3.amazonaws.com/my-bucket/key/for/item");
    }
    
    [Fact]
    public void GetHttpUri_Regionalised_Region_NoKey_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", region: "eu-west-1");

        var s3Uri = objectInBucket.GetHttpUri();

        s3Uri.ToString().Should().Be("https://my-bucket.s3.eu-west-1.amazonaws.com/");
    }
    
    [Fact]
    public void GetHttpUri_Regionalised_Region_Key_Correct()
    {
        var objectInBucket = new RegionalisedObjectInBucket("my-bucket", "key/for/item", "eu-west-1");

        var s3Uri = objectInBucket.GetHttpUri();

        s3Uri.ToString().Should().Be("https://my-bucket.s3.eu-west-1.amazonaws.com/key/for/item");
    }
    
    [Theory]
    [InlineData("bucket", "bucket", "key", "key", true)]
    [InlineData("bucket", "bucket", null, null, true)]
    [InlineData("bucket1", "bucket", "key", "key", false)]
    [InlineData("bucket", "bucket1", "key", "key", false)]
    [InlineData("bucket", "bucket", "key1", "key", false)]
    [InlineData("bucket", "bucket", "key", "key1", false)]
    [InlineData("bucket", "bucket", null, "key", false)]
    [InlineData("bucket", "bucket", "key", null, false)]
    public void EqualsOperator_Compares_Values(string b1, string b2, string k1, string k2, bool expected)
    {
        var objectInBucket1 = new ObjectInBucket(b1, k1);
        var objectInBucket2 = new ObjectInBucket(b2, k2);

        objectInBucket1.Equals(objectInBucket2).Should().Be(expected);
        (objectInBucket1 == objectInBucket2).Should().Be(expected);
        (objectInBucket1 != objectInBucket2).Should().Be(!expected);
    }

    [Theory]
    [InlineData("bucket", "bucket", "key", "key", "region", "region", true)]
    [InlineData("bucket", "bucket", null, null, "region", "region", true)]
    [InlineData("bucket", "bucket", "key", "key", null, null, true)]
    [InlineData("bucket1", "bucket", "key", "key", "region", "region", false)]
    [InlineData("bucket", "bucket1", "key", "key", "region", "region", false)]
    [InlineData("bucket", "bucket", "key1", "key", "region", "region", false)]
    [InlineData("bucket", "bucket", "key", "key1", "region", "region", false)]
    [InlineData("bucket", "bucket", null, "key", "region", "region", false)]
    [InlineData("bucket", "bucket", "key", null, "region", "region", false)]
    [InlineData("bucket", "bucket", "key", "key", "region1", "region", false)]
    [InlineData("bucket", "bucket", "key", "key", "region", "region1", false)]
    [InlineData("bucket", "bucket", "key", "key", null, "region", false)]
    [InlineData("bucket", "bucket", "key", "key", "region", null, false)]
    public void RegionalisedEqualsOperator_Compares_Values(string b1, string b2, string k1, string k2, string r1,
        string r2, bool expected)
    {
        var objectInBucket1 = new RegionalisedObjectInBucket(b1, k1, r1);
        var objectInBucket2 = new RegionalisedObjectInBucket(b2, k2, r2);

        objectInBucket1.Equals(objectInBucket2).Should().Be(expected);
        (objectInBucket1 == objectInBucket2).Should().Be(expected);
        (objectInBucket1 != objectInBucket2).Should().Be(!expected);
    }
}