using DLCS.AWS.Settings;
using DLCS.Core.Settings;

namespace API.Settings
{
    public class ApiSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public DlcsSettings DLCS { get; set; }
        
        public AWSSettings AWS { get; set; }
        
        public string PathBase { get; set; }
        
        public string Salt { get; set; }
    }
}