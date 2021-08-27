using System.Collections.Generic;
using System.IO;
using DLCS.Core.Types;
using DLCS.Model.Templates;
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
        /// Folder template for downloading pointing ImageServer at local file
        /// </summary>
        public string ImageFolderTemplateImageServer { get; set; }
        
        /// <summary>
        /// Folder template for downloading resources to.
        /// </summary>
        public string ImageFolderTemplateOrchestrator { get; set; }
        
        /// <summary>
        /// If true, requests for info.json will cause image to be orchestrated.
        /// </summary>
        public bool OrchestrateOnInfoJson { get; set; }

        public ProxySettings Proxy { get; set; }
        
        public CacheSettings Caching { get; set; }

        /// <summary>
        /// Get the local folder where Asset should be saved to
        /// </summary>
        public string GetImageLocalPath(AssetId assetId, bool forImageServer)
        {
            var template = forImageServer ? ImageFolderTemplateImageServer : ImageFolderTemplateOrchestrator;
            var separator = forImageServer ? '/' : Path.DirectorySeparatorChar;
            return TemplatedFolders.GenerateTemplate(template, assetId, separator);
        }
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
        
        /// <summary>
        /// The root URI of the image server
        /// </summary>
        public string ImageServerRoot { get; set; }
        
        /// <summary>
        /// Whether resizing thumbs is supported
        /// </summary>
        public bool CanResizeThumbs { get; set; }
        
        /// <summary>
        /// Get the root path that thumb handler is listening on
        /// </summary>
        public string ThumbResizePath { get; set; } = "thumbs";

        /// <summary>
        /// A collection of resize config for serving resized thumbs rather than handling requests via image-server
        /// </summary>
        public Dictionary<string, ThumbUpscaleConfig> ThumbUpscaleConfig { get; set; } = new();
    }

    /// <summary>
    /// Represents resize logic for a set of assets
    /// </summary>
    public class ThumbUpscaleConfig
    {
        /// <summary>
        /// Regex to validate image Id against, the entire asset Id will be used (e.g. 2/2/my-image-name)
        /// </summary>
        public string AssetIdRegex { get; set; }

        /// <summary>
        /// The maximum % size difference for upscaling.
        /// </summary>
        public int UpscaleThreshold { get; set; }
    }
}