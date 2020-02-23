namespace DLCS.Model.Storage
{
    /// <summary>
    /// Not S3-specific, but doesn't hide storage of objects in buckets
    /// </summary>
    public class ObjectInBucket
    {
        public string Bucket { get; set; }
        public string Key { get; set; }

        public ObjectInBucket Clone()
        {
            return new ObjectInBucket
            {
                Bucket = Bucket,
                Key = Key
            };
        }

        public override string ToString() => $"{Bucket}:::{Key}";
    }
}
