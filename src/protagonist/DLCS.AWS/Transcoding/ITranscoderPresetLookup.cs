using DLCS.AWS.Transcoding.Models;

namespace DLCS.AWS.Transcoding;

/// <summary>
/// Interface for working with AWS ElasticTranscoder presets
/// </summary>
public interface ITranscoderPresetLookup
{
    /// <summary>
    /// Get a lookup of preset {policy-name}:{preset}
    /// (e.g. 'video-hd': {'System-Generic_Hd_Mp4', 'video-hd', 'mp4'})
    /// </summary>
    /// <returns>Dictionary presets, keyed by name, where 'name' is used in DeliveryChannel policy</returns>
    Dictionary<string, TranscoderPreset> GetPresetLookupByPolicyName();
    
    /// <summary>
    /// Get a lookup of preset {id}:{preset}
    /// (e.g. 'System-Generic_Hd_Mp4': {'System-Generic_Hd_Mp4', 'video-hd', 'mp4'})
    /// </summary>
    /// <returns>Dictionary presets, keyed by id, where 'id' is used in Transcoding system</returns>
    Dictionary<string, TranscoderPreset> GetPresetLookupById();
}
