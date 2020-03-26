namespace DLCS.Repository.Settings
{
    public class ThumbsSettings
    {
        public bool EnsureNewThumbnailLayout { get; set; } = false;
        
        public string ThumbsBucket { get; set; }

        public class Constants
        {
            /// <summary>
            /// Key of the json file that contains available sizes.
            /// </summary>
            public const string SizesJsonKey = "s.json";

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