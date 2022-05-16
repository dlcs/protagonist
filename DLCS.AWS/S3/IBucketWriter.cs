using System.IO;
using System.Threading.Tasks;
using DLCS.AWS.S3.Models;

namespace DLCS.AWS.S3
{
    /// <summary>
    /// Interface wrapping write interactions with cloud blob storage.
    /// </summary>
    public interface IBucketWriter
    {
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