namespace DLCS.AWS.Settings
{
    /// <summary>
    /// Strongly typed S3 settings object 
    /// </summary>
    public class S3Settings
    {
        /// <summary>
        /// Name of bucket storing pre-generated thumbnails
        /// </summary>
        public string ThumbsBucket { get; set; }
        
        /// <summary>
        /// Name of bucket for storing generated output (e.g. NamedQuery results)
        /// </summary>
        public string OutputBucket { get; set; }
        
        /// <summary>
        /// Name of bucket used when the DLCS provides the origin of asset itself, by hosting, rather than
        /// relying on a third party bucket or other origin.
        /// DLCS applications like API and Portal will deposit uploaded resources here.
        /// </summary>
        public string OriginBucket { get; set; }
        
        /// <summary>
        /// Service root for S3. Only used if running LocalStack
        /// </summary>
        public string ServiceUrl { get; set; } = "http://localhost:4566/";
        
    }
}