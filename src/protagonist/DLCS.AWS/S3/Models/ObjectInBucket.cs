using DLCS.Core.Guard;

namespace DLCS.AWS.S3.Models
{
    /// <summary>
    /// Not S3-specific, but doesn't hide storage of objects in buckets
    /// </summary>
    public class ObjectInBucket
    {
        /// <summary>
        /// The Bucket that this object is for
        /// </summary>
        public string Bucket { get; }
        
        /// <summary>
        /// The object key within Bucket that this object is for.
        /// </summary>
        public string? Key { get; }

        public ObjectInBucket(string bucket, string? key = null)
        {
            Bucket = bucket.ThrowIfNullOrWhiteSpace(nameof(bucket));
            Key = key;
        }

        /// <summary>
        /// Create a copy if ObjectInBucket with a new key.
        /// </summary>
        /// <param name="key">New Key to use.</param>
        /// <returns>New ObjectInBucket object, same Bucket property but new Key.</returns>
        public ObjectInBucket CloneWithKey(string key) => new(Bucket, key);

        public override string ToString() => $"{Bucket}:::{Key}";
    }
}
