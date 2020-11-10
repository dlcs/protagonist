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
        
        /// <summary>
        /// Write content from provided stream to S3
        /// </summary>
        Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string? contentType = null);

        /// <summary>
        /// Delete specified objects underlying storage.
        /// NOTE: This method assumes all objects are in the same bucket.
        /// </summary>
        /// <param name="toDelete">List of objects to delete</param>
        Task DeleteFromBucket(params ObjectInBucket[] toDelete);
    }
}
