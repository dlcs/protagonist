using System.Text.RegularExpressions;
using DLCS.AWS.S3;
using DLCS.Core.Types;

namespace Engine.Ingest.Timebased;

public static class TranscoderTemplates
{
    // technical details will contain a comma separated list of presets to use with the extension in brackets at the end
    // e.g. Wellcome Standard MP4(mp4),Wellcome Standard WebM(webm)
    private static readonly Regex PresetRegex = new(@"^(.*?)\((.*?)\)$", RegexOptions.Compiled);

    /// <summary>
    /// Get the destination path where transcoded asset should be output to and the cleaned up presetName. 
    /// </summary>
    /// <param name="mediaType">The media-type/content-type for asset.</param>
    /// <param name="assetId">Id of asset being ingested.</param>
    /// <param name="preset">The preset id from ImageOptimisationPolicy</param>
    /// <param name="jobId">Unique identifier for job</param>
    /// <returns></returns>
    public static (string? template, string? presetName) ProcessPreset(string mediaType, AssetId assetId, string preset,
        string jobId)
    {
        var match = PresetRegex.Match(preset);

        if (!match.Success) return (null, null);

        var presetName = match.Groups[1].Value;
        var presetExtension = match.Groups[2].Value;
        var template = GetDestinationTemplate(mediaType);

        var path = template
            .Replace("{jobId}", jobId)
            .Replace("{asset}", S3StorageKeyGenerator.GetStorageKey(assetId))
            .Replace("{extension}", presetExtension);
        return (path, presetName);
    }

    /// <summary>
    /// Extract the final destination S3 storage key from the transcoder output key 
    /// </summary>
    /// <param name="outputKey">S3 key where transcoded output is stored</param>
    /// <returns>Final key</returns>
    public static string GetFinalDestinationKey(string outputKey) =>
        outputKey.Substring(outputKey.IndexOf("/", StringComparison.Ordinal) + 1);

    private static string GetDestinationTemplate(string mediaType)
    {
        // audio: {customer}/{space}/{image}/full/max/default.{extension} (mediatype like audio/)
        // video: {customer}/{space}/{image}/full/full/max/max/0/default.{extension} (mediatype like video/)
        if (mediaType.StartsWith("audio/"))
        {
            return "{jobId}/{asset}/full/max/default.{extension}";
        }
            
        if (mediaType.StartsWith("video/"))
        {
            return "{jobId}/{asset}/full/full/max/max/0/default.{extension}";
        }

        throw new InvalidOperationException($"Unable to determine target location for mediaType '{mediaType}'");
    }
}