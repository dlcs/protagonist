namespace DLCS.AWS.Settings
{
    /// <summary>
    /// Strongly typed S3 settings object 
    /// </summary>
    public class S3Settings
    {
        /// <summary>
        /// Name of bucket storing IIIF presentation items
        /// </summary>
        public string OutputBucket { get; set; }
        
        /// <summary>
        /// Name of bucket storing pre-generated thumbnails
        /// </summary>
        public string ThumbsBucket { get; set; }
        
        /// <summary>
        /// Service root for S3. Only used 
        /// </summary>
        public string ServiceUrl { get; set; } = "http://localhost:4566/";
    }
}