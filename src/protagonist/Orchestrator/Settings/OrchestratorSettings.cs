using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Caching;
using DLCS.Web.Response;

namespace Orchestrator.Settings;

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
    /// Which image-server is handling downstream tile requests
    /// </summary>
    /// <remarks>
    /// Ideally the Orchestrator should be agnostic to this but, for now at least, the downstream image server will
    /// be useful to know for toggling some functionality
    /// </remarks>
    public ImageServer ImageServer { get; set; } = ImageServer.Cantaloupe;

    /// <summary>
    /// Configuration for specifying paths to images stored on fast disk.
    /// </summary>
    public Dictionary<ImageServer, ImageServerConfig> ImageServerPathConfig { get; set; } = new();

    /// <summary>
    /// The current <see cref="ImageServerConfig"/> object
    /// </summary>
    public ImageServerConfig ImageServerConfig => ImageServerPathConfig[ImageServer];

    /// <summary>
    /// Folder template for downloading resources to.
    /// </summary>
    public string ImageFolderTemplateOrchestrator { get; set; }

    /// <summary>
    /// If true, requests for info.json will cause image to be orchestrated.
    /// </summary>
    public bool OrchestrateOnInfoJson { get; set; } = true;

    /// <summary>
    /// If <see cref="OrchestrateOnInfoJson"/> is true, this is the max number of requests that will be honoured
    /// </summary>
    public int OrchestrateOnInfoJsonMaxCapacity { get; set; } = 50;

    /// <summary>
    /// String used for salting requests to API
    /// </summary>
    public string ApiSalt { get; set; }

    /// <summary>
    /// Default Presentation API Version to conform to when returning presentation resources  
    /// </summary>
    public IIIF.Presentation.Version DefaultIIIFPresentationVersion { get; set; } = IIIF.Presentation.Version.V3;
    
    /// <summary>
    /// Default Image API Version to conform to when returning image description resources
    /// </summary>
    public IIIF.ImageApi.Version DefaultIIIFImageVersion { get; set; } = IIIF.ImageApi.Version.V3;
    
    /// <summary>
    /// Root URL for dlcs api
    /// </summary>
    public Uri ApiRoot { get; set; }

    /// <summary>
    /// The thumbnail that is the default target size for rendering manifests such as NQs. We won't necessarily
    /// render a thumbnail of this size but will aim to get as close as possible.
    /// </summary>
    public int TargetThumbnailSize { get; set; } = 200;

    public ReingestOnOrchestrationSettings ReingestOnOrchestration { get; set; } = new();

    public ProxySettings Proxy { get; set; } = new();

    public CacheSettings Caching { get; set; } = new();

    public AuthSettings Auth { get; set; } = new();

    public NamedQuerySettings NamedQuery { get; set; } = new();
}

public class ProxySettings
{
    /// <summary>
    /// Get the root path that thumb handler is listening on
    /// </summary>
    public string ThumbsPath { get; set; } = "thumbs";

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

    /// <summary>
    /// A collection of resize config for serving resized thumbs rather than handling requests via image-server
    /// </summary>
    public Dictionary<string, ThumbUpscaleConfig> ThumbUpscaleConfig { get; set; } = new();

    /// <summary>
    /// If true details of proxied location are added as x-proxy-* headers. Intended for debug use only.
    /// </summary>
    public bool AddProxyDebugHeaders { get; set; } = false;
    
    /// <summary>
    /// If true, Yarp will proxy to S3 optimised origins using presigned URLs.
    /// Else, Yarp will proxy to the S3 https URL directly. 
    /// </summary>
    /// <remarks>
    /// Direct URL access requires the object to be publicly available, or for a bucket policy to be configured to allow
    /// access, e.g. with aws:sourceVpce or aws:sourceVpc access allowed.
    /// </remarks>
    public bool UsePresignedUrlsForOptimised { get; set; } = false;
    
    /// <summary>
    /// If true, Yarp will proxy to S3 optimised origins using presigned URLs.
    /// Else, Yarp will proxy to the S3 https URL directly. 
    /// </summary>
    /// <remarks>
    /// Direct URL access requires the object to be publicly available, or for a bucket policy to be configured to allow
    /// access, e.g. with aws:sourceVpce or aws:sourceVpc access allowed.
    /// </remarks>
    public bool UsePresignedUrlsForDlcs { get; set; } = false;

    /// <summary>
    /// Number of seconds for which PresignedUrls are valid. Only used if UsePresignedUrlsForProxy = true
    /// </summary>
    public int PresignedUrlExpirySecs { get; set; } = 600;
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

    /// <summary>
    /// A list of supported auth profiles for Access Cookie Services that are non-standard.
    /// </summary>
    public List<string> SupportedAccessCookieProfiles { get; set; } =
        new() { "http://iiif.io/api/auth/0/login/clickthrough" };

    /// <summary>
    /// A boolean controlling whether to throw an exception if an unsupported auth Profile is encountered, either from
    /// IIIF spec or <see cref="SupportedAccessCookieProfiles"/>. If false then null returned
    /// </summary>
    public bool ThrowIfUnsupportedProfile { get; set; } = false;
    
    /// <summary>
    /// URI template configuration for auth paths 
    /// </summary>
    public PathTemplateOptions AuthPathRules { get; set; }
    
    /// <summary>
    /// URI template configurations for IIIF Auth 2.0 paths
    /// </summary>
    public TypedPathTemplateOptions Auth2PathRules { get; set; }
    
    /// <summary>
    /// URL root for service handling IIIF Authorization 2.0 flow requests
    /// </summary>
    public Uri Auth2ServiceRoot { get; set; }

    /// <summary>
    /// Timeout, in seconds, for Http requests to auth service
    /// </summary>
    public int AuthTimeoutSecs { get; set; } = 5;
}

/// <summary>
/// Settings related to NamedQuery generation and serving
/// </summary>
public class NamedQuerySettings
{
    /// <summary>
    /// Number of seconds after which an "InProcess" control file is considered stale for.
    /// After this time has elapsed it will be recreated.
    /// </summary>
    public int ControlStaleSecs { get; set; } = 600;

    /// <summary>
    /// URL root of fireball service for PDF generation
    /// </summary>
    public Uri FireballRoot { get; set; }

    /// <summary>
    /// Folder template for creating local Zip file
    /// </summary>
    public string ZipFolderTemplate { get; set; }
    
    /// <summary>
    /// Customer-specific overrides; keyed by customer Id
    /// </summary>
    public Dictionary<string, CustomerOverride> CustomerOverrides { get; set; } = new();
}

public class CustomerOverride
{
    /// <summary>
    /// List of roles that are whitelisted for PDF inclusion.
    /// When generating a PDF, images with roles are redacted, unless the role is on whitelist.
    /// When serving, if a PDF contains an image that has a role on the whitelist - authentication will be required.
    /// </summary>
    public List<string> PdfRolesWhitelist { get; set; } = new();
}

/// <summary>
/// Enum representing image server used for serving image requests
/// </summary>
public enum ImageServer
{
    /// <summary>
    /// Cantaloupe image server
    /// </summary>
    Cantaloupe,

    /// <summary>
    /// IIP Image Server
    /// </summary>
    IIPImage
}

/// <summary>
/// Represents redirect configuration for redirecting ImageServer requests
/// </summary>
public class ImageServerConfig
{
    /// <summary>
    /// Directory separator character to use when specifying path to image.
    /// Used when constructing {image-dir} template replacement.
    /// </summary>
    public string Separator { get; set; }

    /// <summary>
    /// Path template for sending requests to image server.
    /// Supports {customer}, {space}, {image-dir} and {image} replacements.
    /// </summary>
    /// <remarks>
    /// This is the template used to construct requests to image servers. 
    /// </remarks>
    public string PathTemplate { get; set; }

    /// <summary>
    /// The prefix for forwarding requests to this image-server by supported Image Api version. The final URL sent
    /// to the image-server is {image-server-root}{url-prefix}{path-template}{image-request}
    /// </summary>
    public Dictionary<IIIF.ImageApi.Version, string> VersionPathTemplates { get; set; }

    private string? defaultVersionPathTemplate;

    /// <summary>
    /// The default version path template to use for non-versioned requests.
    /// This is the VersionPathTemplates list with highest version.
    /// </summary>
    public string DefaultVersionPathTemplate
    {
        get
        {
            if (string.IsNullOrEmpty(defaultVersionPathTemplate))
            {
                // On first request find the value with highest Version
                defaultVersionPathTemplate = VersionPathTemplates.MaxBy(t => t.Key switch
                {
                    IIIF.ImageApi.Version.Unknown => 1,
                    IIIF.ImageApi.Version.V2 => 2,
                    IIIF.ImageApi.Version.V3 => 3,
                    _ => throw new ArgumentOutOfRangeException(nameof(VersionPathTemplates),
                        "Unknown IIIFImageVersion")
                }).Value;
            }

            return defaultVersionPathTemplate;
        }
    }
}

/// <summary>
/// Settings related to reingesting on orchestration
/// </summary>
public class ReingestOnOrchestrationSettings
{
    /// <summary>
    /// This date controls how images without ImageLocation.S3 are handled.
    /// For Images created after this date, return a 404
    /// For Images created before this date, reingest  
    /// </summary>
    /// <remarks>See https://github.com/dlcs/protagonist/issues/505</remarks>
    public DateTime? EmptyImageLocationCreatedDate { get; set; }
}