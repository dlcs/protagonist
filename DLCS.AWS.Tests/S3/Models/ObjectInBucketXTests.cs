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

            s3Uri.Should().Be("s3://my-bucket/");
        }
        
        [Fact]
        public void GetS3Uri_Key_Correct()
        {
            var objectInBucket = new ObjectInBucket("my-bucket", "key/for/item");

            var s3Uri = objectInBucket.GetS3Uri();

            s3Uri.Should().Be("s3://my-bucket/key/for/item");
        }
    }
}