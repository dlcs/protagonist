using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Model.Assets;
using Newtonsoft.Json;

namespace Test.Helpers.Integration;

public static class S3TestDataPopulation
{
    public static Task AddSizesJson(this IAmazonS3 amazonS3, string assetId, ThumbnailSizes thumbnailSizes)
    {
        return amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{assetId}/s.json",
            BucketName = "protagonist-thumbs",
            ContentBody = JsonConvert.SerializeObject(thumbnailSizes)
        });
    }
}