namespace DLCS.AWS.Transcoding.Models;

/// <summary>
/// Stores details of transcode preset and any details we need to know about it.
/// </summary>
/// <param name="Id">Identifier for this preset from transcoding system</param>
/// <param name="PolicyName">Name for this preset used for policy data</param>
/// <param name="Extension">File extension used by this preset</param>
public record TranscoderPreset(string Id, string PolicyName, string Extension);
