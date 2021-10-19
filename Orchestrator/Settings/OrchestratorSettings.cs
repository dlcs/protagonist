using System;
using System.Collections.Generic;
using System.IO;
using DLCS.Core.Types;
using DLCS.Model.Templates;
using DLCS.Repository.Caching;

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
        
        /// <summary>
        /// String used for salting requests to API
        /// </summary>
        public string ApiSalt { get; set; }

        /// <summary>
        /// Default Presentation API Version to conform to when returning resources from 
        /// </summary>
        public string DefaultIIIFPresentationVersion { get; set; } = "3.0";

        /// <summary>
        /// Get default Presentation API Version to conform to when returning resources from as enum.
        /// Defaults to V3 if unsupported, or unknown version specified
        /// </summary>
        public IIIF.Presentation.Version GetDefaultIIIFPresentationVersion() 
            => DefaultIIIFPresentationVersion[1] == '2' ? IIIF.Presentation.Version.V2 : IIIF.Presentation.Version.V3;

        /// <summary>
        /// Root URL for dlcs api
        /// </summary>
        public Uri ApiRoot { get; set; }

        /// <summary>
        /// The thumbnail that is the default target size for rendering manifests such as NQs. We won't necessarily
        /// render a thumbnail of this size but will aim to get as close as possible.
        /// </summary>
        public int TargetThumbnailSize { get; set; } = 200;

        public ProxySettings Proxy { get; set; }
        
        public CacheSettings Caching { get; set; }
        
        public AuthSettings Auth { get; set; }

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
        /// Get the root path for serving images
        /// </summary>
        public string ImagePath { get; set; } = "iiif-img";
        
        public NamedQuerySettings NamedQuery { get; set; }

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

    public class AuthSettings
    {
        /// <summary>
        /// Format of authToken, used to generate token id.
        /// {0} is replaced with customer id
        /// </summary>
        public string CookieNameFormat { get; set; } = "dlcs-token-{0}";
        
        /// <summary>
        /// A list of domains to set on auth cookie.
        /// </summary>
        public List<string> CookieDomains { get; set; } = new();

        /// <summary>
        /// If true the current domain is automatically added to auth token domains.
        /// </summary>
        public bool UseCurrentDomainForCookie { get; set; } = true;
    }

    /// <summary>
    /// Settings related to NamedQuery generation and serving
    /// </summary>
    public class NamedQuerySettings
    {
        /// <summary>
        /// Name of S3Bucket for storing PDF output
        /// </summary>
        public string PdfBucket { get; set; }

        /// <summary>
        /// String format for pdf control-file key.
        /// Supported replacements are {customer}/{queryname}/{args}
        /// </summary>
        public string PdfControlFileTemplate { get; set; } = "{customer}/{queryname}/{args}";
    }
}