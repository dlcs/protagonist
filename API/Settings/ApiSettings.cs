using DLCS.Core.Settings;

namespace API.Settings
{
    public class ApiSettings
    {
        /// <summary>
        /// The base URI of DLCS to hand-off requests to.
        /// </summary>
        public DlcsSettings DLCS { get; set; }
        
        public AwsSettings AWS { get; set; }
        
        public string PathBase { get; set; }
        
        public string Salt { get; set; }
    }

    public class AwsSettings
    {
        public string Region { get; set; }
    }
}