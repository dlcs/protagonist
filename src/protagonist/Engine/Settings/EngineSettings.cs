namespace Engine.Settings;

public class EngineSettings
{
    public ImageIngestSettings? ImageIngest { get; set; }

    public TimebasedIngestSettings? TimebasedIngest { get; set; }

    /// <summary>
    /// A collection of customer-specific overrides, keyed by customerId.
    /// </summary> 
    public Dictionary<string, CustomerOverridesSettings> CustomerOverrides { get; set; } = new();
    
    /// <summary>
    /// Template for location to download any temporary assets to disk. Supports standard replacements
    /// {customer}, {space}, {image}
    /// </summary>
    public string DownloadTemplate { get; set; }

    /// <summary>
    /// Get CustomerSpecificSettings, if found. 
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <returns>Customer specific overrides, or default if not found.</returns>
    public CustomerOverridesSettings GetCustomerSettings(int customerId)
        => CustomerOverrides.TryGetValue(customerId.ToString(), out var settings)
            ? settings
            : CustomerOverridesSettings.Empty;
}

/// <summary>
/// Settings directly related to image ingestion
/// </summary>
/// <remarks>These are for use with Tizer/Appetiser settings.</remarks>
public class ImageIngestSettings
{
    /// <summary>
    /// Path template for where file will be copied to
    /// </summary>
    public string SourceTemplate { get; set; }

    /// <summary>
    /// Path template for where derivatives will be generated into
    /// </summary>
    public string DestinationTemplate { get; set; }

    /// <summary>
    /// Path template for where thumbnail derivatives will generated into
    /// </summary>
    public string ThumbsTemplate { get; set; }

    /// <summary>
    /// Whether to use unofficial s3:// format (including region) - required for backwards compat with deliverator
    /// </summary>
    public bool IncludeRegionInS3Uri { get; set; } = false;

    /// <summary>
    /// URI of downstream image processor
    /// </summary>
    public Uri ImageProcessorUrl { get; set; }
    
    /// <summary>
    /// URI of downstream thumbnail processor
    /// </summary>
    public Uri ThumbsProcessorUrl { get; set; }
    
    /// <summary>
    /// Optional path prefix for thumbnail processor. Requested url will be
    /// $"{ThumbsProcessorUrl}/{ThumbsProcessorPathBase}"
    /// </summary>
    public string? ThumbsProcessorPathBase { get; set; } = "iiif/3/";

    /// <summary>
    /// How long, in ms, to delay calling Image-Processor after copying file to shared disk 
    /// </summary>
    public int ImageProcessorDelayMs { get; set; } = 0;

    /// <summary>
    /// Timeout for requests to image-processor 
    /// </summary>
    public int ImageProcessorTimeoutMs { get; set; } = 300000;

    /// <summary>
    /// Root folder for main container
    /// </summary>
    public string ScratchRoot { get; set; }

    /// <summary>
    /// Root folder for use by Image-Processor sidecar
    /// </summary>
    public string ImageProcessorRoot { get; set; }

    /// <summary>
    /// Base url for calling orchestrator.
    /// </summary>
    public Uri OrchestratorBaseUrl { get; set; }

    /// <summary>
    /// Timeout, in ms, to wait for calls to orchestrator
    /// </summary>
    public int OrchestratorTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Default value of whether to orchestrate an image upon ingestion
    /// </summary>
    public bool OrchestrateImageAfterIngest { get; set; }

    /// <summary>
    /// The character to use when replacing an open bracket character
    /// </summary>
    public string OpenBracketReplacement { get; set; } = "_";

    /// <summary>
    /// The character to use when replacing a closing bracket character
    /// </summary>
    public string CloseBracketReplacement { get; set; } = "_";

    /// <summary>
    /// A list of thumbnails that will be added to every asset regardless of the thumbnail policy
    /// </summary>
    public List<string> DefaultThumbs { get; set; } = new();

    /// <summary>
    /// A set of cookie names used by the load balancer to indicate stickiness
    /// </summary>
    public List<string> LoadBalancerStickinessCookieNames { get; set; } = new()
    {
        "AWSALB",
        "AWSALBCORS"
    };

    /// <summary>
    /// Get the root folder, if forImageProcessor will ensure that it is compatible with needs of image-processor
    /// sidecar.
    /// </summary>
    public string GetRoot(bool forImageProcessor = false)
    {
        if (!forImageProcessor) return ScratchRoot;

        return string.IsNullOrEmpty(ImageProcessorRoot)
            ? ScratchRoot
            : ImageProcessorRoot;
    }
}

/// <summary>
/// Settings directly related to A/V ingestion.
/// </summary>
/// <remarks>These will be for ElasticTranscoder</remarks>
public class TimebasedIngestSettings
{
    /// <summary>
    /// The name of the ElasticTranscoder pipeline to use for transcoding AV files
    /// </summary>
    [Obsolete("ElasticTranscode is being replaced by MediaConvert")]
    public string PipelineName { get; set; }
    
    /// <summary>
    /// Name of the MediaConvert queue to use
    /// </summary>
    public MediaConvertSettings MediaConvert { get; set; }
    
    /// <summary>
    /// Mapping of 'friendly' to 'real' transcoder names
    /// </summary>
    public Dictionary<string, string> DeliveryChannelMappings { get; set; } = new();
}

public class MediaConvertSettings
{
    /// <summary>
    /// Name of the MediaConvert queue to use
    /// </summary>
    public required string QueueName { get; set; }

    /// <summary>
    /// Arn of role to use for MediaConvert queue to use
    /// </summary>
    public required string RoleArn { get; set; }
}
