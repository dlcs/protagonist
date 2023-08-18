using DLCS.Core.Guard;

namespace DLCS.AWS.S3.Models;

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
    
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ObjectInBucket)obj);
    }
    
    public override int GetHashCode() => HashCode.Combine(Bucket, Key);

    public static bool operator ==(ObjectInBucket? objectInBucket1, ObjectInBucket? objectInBucket2)
    {
        if (objectInBucket1 is null)
        {
            return objectInBucket2 is null;
        }
        
        if (objectInBucket2 is null)
        {
            return false;
        }
        
        return objectInBucket1.Equals(objectInBucket2);
    }

    public static bool operator !=(ObjectInBucket? objectInBucket1, ObjectInBucket? objectInBucket2) 
        => !(objectInBucket1 == objectInBucket2);

    public override string ToString() => $"{Bucket}:::{Key}";
    
    protected bool Equals(ObjectInBucket other) => Bucket == other.Bucket && Key == other.Key;
}
