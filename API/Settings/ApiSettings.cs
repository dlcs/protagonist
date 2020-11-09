using System;

namespace API.Settings
{
    public class ApiSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public DlcsSettings DLCS { get; set; }
        
        public AwsSettings AWS { get; set; }
    }

    public class DlcsSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public Uri Root { get; set; }

        /// <summary>
        /// Name of the bucket to act as storage origin for uploaded files.
        /// </summary>
        public string OriginBucket { get; set; }
    }

    public class AwsSettings
    {
        public string Region { get; set; }
    }
}