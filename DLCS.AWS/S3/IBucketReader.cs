using DLCS.AWS.S3.Models;

namespace DLCS.AWS.S3
{
    /// <summary>
    /// Interface wrapping read interactions with cloud blob storage.
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
    }
}
