using System;
using System.IO;
using DLCS.Core.Guard;

namespace DLCS.Model.Storage
{
    /// <summary>
    /// Represents an object that has been fetched from a bucket.
    /// </summary>
    public class ObjectFromBucket
    {
        /// <summary>
        /// Gets stream of object.
        /// </summary>
        public Stream? Stream { get; }
        
        /// <summary>
        /// Gets headers associated with this object.
        /// </summary>
        public ObjectInBucketHeaders Headers { get; }
        
        /// <summary>
        /// <see cref="ObjectInBucket"/> used to fetch this object.
        /// </summary>
        public ObjectInBucket ObjectInBucket { get; }

        public ObjectFromBucket(ObjectInBucket objectInBucket, Stream? stream, ObjectInBucketHeaders headers)
        {
            ObjectInBucket = objectInBucket.ThrowIfNull(nameof(objectInBucket));
            Stream = stream ?? Stream.Null;
            Headers = headers ?? new ObjectInBucketHeaders();
        }
    }

    public class ObjectInBucketHeaders
    {
        public string CacheControl { get; set; }
        public string ContentDisposition { get; set; }
        public string ContentEncoding { get; set; }
        public long? ContentLength { get; set; }
        public string ContentMD5 { get; set; }
        public string ContentType { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }
}