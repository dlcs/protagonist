using System;

namespace DLCS.Core.Settings
{
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

        /// <summary>
        /// Default timeout for dlcs api requests.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;
    }
}