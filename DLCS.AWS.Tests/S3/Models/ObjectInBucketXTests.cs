using DLCS.AWS.S3.Models;
using FluentAssertions;
using Xunit;

namespace DLCS.AWS.Tests.S3.Models
{
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
    }
}