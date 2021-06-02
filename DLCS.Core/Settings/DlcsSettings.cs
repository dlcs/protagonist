using System;

namespace DLCS.Core.Settings
{
    public class DlcsSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public Uri ApiRoot { get; set; }
        
        /// <summary>
        /// The base URI for image services and other public-facing resources
        /// </summary>
        public Uri ResourceRoot { get; set; }

        /// <summary>
        /// Name of the bucket to act as storage origin for uploaded files.
        /// </summary>
        public string OriginBucket { get; set; }

        /// <summary>
        /// Default timeout for dlcs api requests.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;
        
        /// <summary>
        /// Safe size of thumbnail to use in the portal
        /// TODO - the DLCS API's returned Image resource should have this kind of information
        /// </summary>
        public int PortalThumb { get; set; }
    }
}