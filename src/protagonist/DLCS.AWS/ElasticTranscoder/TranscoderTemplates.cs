using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Types;

namespace DLCS.AWS.ElasticTranscoder;

public static class TranscoderTemplates
{
    /// <summary>
    /// Get the destination path where transcoded asset should be output to and the cleaned up presetName. 
    /// </summary>
    /// <param name="mediaType">The media-type/content-type for asset.</param>
    /// <param name="assetId">Id of asset being ingested.</param>
    /// <param name="jobId">Unique identifier for job</param>
    /// <param name="presetExtension">The extension to use in the path</param>
    /// <returns></returns>
    public static string? ProcessPreset(string mediaType, AssetId assetId,
        string jobId, string? presetExtension)
    {
        if (presetExtension.IsNullOrEmpty()) return null;
        
        var template = GetDestinationTemplate(mediaType);

        var path = template
            .Replace("{jobId}", jobId)
            .Replace("{asset}", S3StorageKeyGenerator.GetStorageKey(assetId))
            .Replace("{extension}", presetExtension);
        return path;
    }

    /// <summary>
    /// Extract the final destination S3 storage key from the transcoder output key 
    /// </summary>
    /// <param name="outputKey">S3 key where transcoded output is stored</param>
    /// <returns>Final key</returns>
    public static string GetFinalDestinationKey(string outputKey) =>
        outputKey.Substring(outputKey.IndexOf("/", StringComparison.Ordinal) + 1);

    public static string GetDestinationTemplate(string mediaType)
    {
        // audio: {customer}/{space}/{image}/full/max/default.{extension} (mediatype like audio/)
        // video: {customer}/{space}/{image}/full/full/max/max/0/default.{extension} (mediatype like video/)
        if (MIMEHelper.IsAudio(mediaType))
        {
            return "{jobId}/{asset}/full/max/default.{extension}";
        }
            
        if (MIMEHelper.IsVideo(mediaType))
        {
            return "{jobId}/{asset}/full/full/max/max/0/default.{extension}";
        }

        throw new InvalidOperationException($"Unable to determine target location for mediaType '{mediaType}'");
    }
}