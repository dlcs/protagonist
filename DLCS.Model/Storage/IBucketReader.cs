using System.IO;
using System.Threading.Tasks;

namespace DLCS.Model.Storage
{
    public interface IBucketReader
    {
        Task WriteObjectFromBucket(ObjectInBucket objectInBucket, Stream targetStream);
        Task<string[]> GetMatchingKeys(ObjectInBucket rootKey);
        Task CopyWithinBucket(string bucket, string sourceKey, string destKey);
        Task WriteToBucket(ObjectInBucket dest, string content, string contentType);
    }
}
