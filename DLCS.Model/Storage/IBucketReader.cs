using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Storage
{
    /// <summary>
    /// Interface wrapping interactions with cloud blob storage.
    /// </summary>
    public interface IBucketReader
    {
        /// <summary>
        /// Get full object from bucket, including content and headers.
        /// </summary>
        Task<ObjectFromBucket> GetObjectFromBucket(ObjectInBucket objectInBucket,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get specified object from bucket.
        /// </summary>
        /// <param name="objectInBucket">Object to read.</param>
        Task<Stream?> GetObjectContentFromBucket(ObjectInBucket objectInBucket);
        
        /// <summary>
        /// Get a list of all keys within specified root.
        /// </summary>
        /// <param name="rootKey"><see cref="ObjectInBucket"/> </param>
        /// <returns></returns>
        Task<string[]> GetMatchingKeys(ObjectInBucket rootKey);
        
        // TODO - delete these as they are not Reading - move to diff interface, e.g. IBucketWriter?
        /// <summary>
        /// Copy key to new key within same bucket.
        /// </summary>
        Task CopyWithinBucket(string bucket, string sourceKey, string destKey);
        
        /// <summary>
        /// Write content from provided string to S3 
        /// </summary>
        /// <returns></returns>
        Task WriteToBucket(ObjectInBucket dest, string content, string contentType);
        
        /// <summary>
        /// Write content from provided stream to S3
        /// </summary>
        Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string? contentType = null);

        /// <summary>
        /// Write file to S3
        /// </summary>
        Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string? contentType = null);

        /// <summary>
        /// Delete specified objects underlying storage.
        /// NOTE: This method assumes all objects are in the same bucket.
        /// </summary>
        /// <param name="toDelete">List of objects to delete</param>
        Task DeleteFromBucket(params ObjectInBucket[] toDelete);
    }
}
