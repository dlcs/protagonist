using System.Diagnostics.CodeAnalysis;
using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Types;

namespace DLCS.AWS.Transcoding;

// TODO - move this to .Transcoding namespace?
public static class TranscoderTemplates
{
    /// <summary>
    /// Get the destination path where transcoded asset is output to 
    /// </summary>
    /// <param name="mediaType">The media-type/content-type for asset.</param>
    /// <param name="assetId">Id of asset being ingested.</param>
    /// <param name="presetExtension">The extension to use in the path</param>
    [return: NotNullIfNotNull(nameof(presetExtension))]
    public static string? GetTranscodeKey(string mediaType, AssetId assetId,
        string? presetExtension)
    {
        if (presetExtension.IsNullOrEmpty()) return null;
        
        var template = GetDestinationTemplate(mediaType);

        var path = template
            .Replace("{asset}", S3StorageKeyGenerator.GetStorageKey(assetId))
            .Replace("{extension}", presetExtension);
        return path;
    }
    
    /// <summary>
    /// Extract the final destination S3 storage key from the transcoder output key 
    /// </summary>
    /// <param name="outputKey">S3 key where transcoded output is stored</param>
    /// <returns>Final key</returns>
    [Obsolete("ElasticTranscoder")]
    public static string GetFinalDestinationKey(string outputKey) =>
        outputKey.Substring(outputKey.IndexOf("/", StringComparison.Ordinal) + 1);

    /// <summary>
    /// Get destination template for transcoded assets. Contains {asset} and {extension} placeholder values
    /// </summary>
    public static string GetDestinationTemplate(string mediaType)
    {
        // audio: {customer}/{space}/{image}/full/max/default.{extension} (mediatype like audio/)
        // video: {customer}/{space}/{image}/full/full/max/max/0/default.{extension} (mediatype like video/)
        if (MIMEHelper.IsAudio(mediaType))
        {
            return "{asset}/full/max/default.{extension}";
        }
            
        if (MIMEHelper.IsVideo(mediaType))
        {
            return "{asset}/full/full/max/max/0/default.{extension}";
        }

        throw new InvalidOperationException($"Unable to determine target location for mediaType '{mediaType}'");
    }
}
