using System.IO;
using System.Threading.Tasks;

namespace DLCS.Model.Storage
{
    /// <summary>
    /// Interface wrapping interactions with cloud blob storage.
    /// </summary>
    public interface IBucketReader
    {
        /// <summary>
        /// Get specified object from bucket.
        /// </summary>
        /// <param name="objectInBucket">Object to read.</param>
        Task<Stream?> GetObjectFromBucket(ObjectInBucket objectInBucket);
        
        Task<string[]> GetMatchingKeys(ObjectInBucket rootKey);
        
        Task CopyWithinBucket(string bucket, string sourceKey, string destKey);
        
        Task WriteToBucket(ObjectInBucket dest, string content, string contentType);
    }
}
