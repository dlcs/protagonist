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
        /// Write object from bucket to provided stream.
        /// </summary>
        /// <param name="objectInBucket">Object to read.</param>
        /// <param name="targetStream">Stream to write object into.</param>
        Task WriteObjectFromBucket(ObjectInBucket objectInBucket, Stream targetStream);
        
        Task<string[]> GetMatchingKeys(ObjectInBucket rootKey);
        
        Task CopyWithinBucket(string bucket, string sourceKey, string destKey);
        
        Task WriteToBucket(ObjectInBucket dest, string content, string contentType);
    }
}
