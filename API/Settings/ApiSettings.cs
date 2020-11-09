using System;

namespace API.Settings
{
    public class ApiSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public DlcsSettings DLCS { get; set; }
    }
    
    public class DlcsSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public Uri Root { get; set; }
    }
}