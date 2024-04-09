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
    /// URI of downstream derivative processor
    /// </summary>
    public Uri ThumbsProcessorUrl { get; set; }

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
    public string PipelineName { get; set; }

    /// <summary>
    /// Mapping of 'friendly' to 'real' transcoder names
    /// </summary>
    public Dictionary<string, string> DeliveryChannelMappings { get; set; } = new();
}