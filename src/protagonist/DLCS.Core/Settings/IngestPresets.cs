namespace DLCS.Core.Settings;

/// <summary>
/// A collection of presets to use for an ingest
/// </summary>
public class IngestPresets
{
    /// <summary>
    /// A collection of optimisation policies, keyed by type. e.g. "audio"/"video". Key is "*" for all.
    /// </summary>
    public string OptimisationPolicy { get; }
    
    /// <summary>
    /// Default delivery-channel for family
    /// </summary>
    public string DeliveryChannel { get; }
    
    /// <summary>
    /// Default thumbnail policy for family
    /// </summary>
    public string ThumbnailPolicy { get; }

    public IngestPresets(string optimisationPolicy, string deliveryChannel, string thumbnailPolicy)
    {
        OptimisationPolicy = optimisationPolicy;
        DeliveryChannel = deliveryChannel;
        ThumbnailPolicy = thumbnailPolicy;
    }
}