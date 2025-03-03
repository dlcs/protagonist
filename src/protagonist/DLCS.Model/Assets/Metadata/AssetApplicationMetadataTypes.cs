namespace DLCS.Model.Assets.Metadata;

public static class AssetApplicationMetadataTypes
{
    /// <summary>
    /// Stores generated thumbnail dimensions, split by "open" and "auth". See <see cref="ThumbnailSizes"/>
    /// </summary>
    public const string ThumbSizes = "ThumbSizes";
    
    /// <summary>
    /// Stores details of transcoded AV derivatives - location, dimensions, extension etc. See <see cref="AVTranscode"/>
    /// </summary>
    public const string AVTranscodes = "AVTranscodes";
}
