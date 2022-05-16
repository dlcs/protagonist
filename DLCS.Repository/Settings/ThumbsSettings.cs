namespace DLCS.Repository.Settings
{
    /// <summary>
    /// Options to manage configuration of thumbnails
    /// </summary>
    public class ThumbsSettings
    {
        /// <summary>
        /// If true, when a request is received old thumbnail layout will be rearranged to match new.
        /// </summary>
        public bool EnsureNewThumbnailLayout { get; set; } = false;
        
        /// <summary>
        /// The name of the bucket containing thumbnail jpg.
        /// </summary>
        public string ThumbsBucket { get; set; }
        
        /// <summary>
        /// If true the service will attempt to resize an existing jpg to serve images.
        /// </summary>
        public bool Resize { get; set; }
        
        /// <summary>
        /// If true, smaller thumbnails will be upscaled to handle non-matching requests.
        /// This is ignored if Resize=False
        /// </summary>
        public bool Upscale { get; set; }
        
        /// <summary>
        /// The maximum % size difference for upscaling.
        /// </summary>
        public int UpscaleThreshold { get; set; }

        public class Constants
        {
            /// <summary>
            /// S3 slug where open thumbnails are stored.
            /// </summary>
            public const string OpenSlug = "open";

            /// <summary>
            /// S3 slug where thumbnails requiring authorisation are stored.
            /// </summary>
            public const string AuthorisedSlug = "auth";
        }
    }
}