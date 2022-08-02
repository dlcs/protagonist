using DLCS.AWS.S3.Models;

namespace DLCS.AWS.S3;

/// <summary>
/// Interface wrapping write interactions with cloud blob storage.
/// </summary>
public interface IBucketWriter
{
    /// <summary>
    /// Copy bucket object from source to destination
    /// </summary>
    Task CopyObject(ObjectInBucket source, ObjectInBucket destination);
    
    /// <summary>
    /// Copy a large file between buckets using multi part upload.
    /// This should always be used for files >5GiB
    /// </summary>
    /// <param name="source">Bucket where object is currently stored.</param>
    /// <param name="destination">Target bucket where object is to be stored.</param>
    /// <param name="verifySize">Function to verify objectSize prior to copying. Not copied if false returned.</param>
    /// <param name="destIsPublic">If true the copied object is given public access rights</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>ResultStatus signifying success or failure alongside ContentSize</returns>
    /// <remarks>See https://docs.aws.amazon.com/AmazonS3/latest/dev/CopyingObjctsUsingLLNetMPUapi.html </remarks>
    public Task<LargeObjectCopyResult> CopyLargeObject(ObjectInBucket source, ObjectInBucket destination,
        Func<long, Task<bool>>? verifySize = null, bool destIsPublic = false, CancellationToken token = default);

    /// <summary>
    /// Write content from provided string to S3 
    /// </summary>
    /// <returns></returns>
    Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Write content from provided stream to S3
    /// </summary>
    Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string? contentType = null);

    /// <summary>
    /// Write file to S3
    /// </summary>
    Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string? contentType = null,
        CancellationToken token = default);

    /// <summary>
    /// Delete specified objects underlying storage.
    /// NOTE: This method assumes all objects are in the same bucket.
    /// </summary>
    /// <param name="toDelete">List of objects to delete</param>
    Task DeleteFromBucket(params ObjectInBucket[] toDelete);
}

/// <summary>
/// Represents the result of a bucket to bucket copy operation
/// </summary>
/// <param name="Result"><see cref="LargeObjectStatus"/> object that represents overall result of the copy</param>
/// <param name="Size">The size of the asset copied</param>
public record LargeObjectCopyResult(LargeObjectStatus Result, long? Size = null)
{
    /// <summary>
    /// Value indicating whether the destination key exists - only set in NotFound responses 
    /// </summary>
    public bool? DestinationExists { get; set; }
}

/// <summary>
/// The overall result of a bucket to bucket copy operation
/// </summary>
public enum LargeObjectStatus
{
    /// <summary>
    /// Default value
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Object was copied successfully
    /// </summary>
    Success,
    
    /// <summary>
    /// Copy operation was cancelled - this may result in incomplete multi-part uploads being left in S3  
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// Any error occurred during copy
    /// </summary>
    Error,
    
    /// <summary>
    /// File exceeded allowed storage limits
    /// </summary>
    FileTooLarge,
    
    /// <summary>
    /// Unable to copy as target file not found
    /// </summary>
    SourceNotFound
}