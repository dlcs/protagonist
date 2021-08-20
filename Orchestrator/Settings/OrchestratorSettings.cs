
using DLCS.Repository.Settings;

namespace Orchestrator.Settings
{
    public class OrchestratorSettings
    {
        /// <summary>
        /// PathBase to host app on.
        /// </summary>
        public string PathBase { get; set; }
        
        /// <summary>
        /// Regex for S3-origin, for objects uploaded directly to DLCS.
        /// </summary>
        public string S3OriginRegex { get; set; }
        
        /// <summary>
        /// URI template for auth services
        /// </summary>
        public string AuthServicesUriTemplate { get; set; }

        /// <summary>
        /// Timeout for critical orchestration path. How long to wait to acheive lock when orchestrating asset.
        /// If timeout breached, multiple orchestrations can happen for same item.
        /// </summary>
        public int CriticalPathTimeoutMs { get; set; } = 10000;
        
        /// <summary>
        /// Folder template for downloading resources to.
        /// </summary>
        public string ImageFolderTemplate { get; set; }

        public ProxySettings Proxy { get; set; }
        
        public CacheSettings Caching { get; set; }
    }

    public class ProxySettings
    {
        /// <summary>
        /// Get the root path that thumb handler is listening on
        /// </summary>
        public string ThumbsPath { get; set; } = "thumbs";

        /// <summary>
        /// Get value of whether to check for UV thumbs (90,)
        /// </summary>
        public bool CheckUVThumbs { get; set; } = true;

        /// <summary>
        /// Get Size parameter for any UV thumb redirect.
        /// </summary>
        public string UVThumbReplacementPath { get; set; } = "!200,200";
        
        /// <summary>
        /// Get https base url for region.
        /// </summary>
        public string S3HttpBase { get; set; }
        
        /// <summary>
        /// Get the S3 storage bucket name.
        /// </summary>
        public string StorageBucket { get; set; }
    }
}