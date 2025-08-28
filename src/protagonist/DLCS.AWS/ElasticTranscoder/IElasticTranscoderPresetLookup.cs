using DLCS.AWS.Transcoding.Models;

namespace DLCS.AWS.ElasticTranscoder;

/// <summary>
/// Interface for working with AWS ElasticTranscoder presets
/// </summary>
public interface IElasticTranscoderPresetLookup
{
    /// <summary>
    /// Get a lookup of preset {name}:{preset}
    /// (e.g. "System Preset: Generic 1080p": {1351620000001-000001, 'System Preset: Generic 1080p', 'mp4'})
    /// </summary>
    /// <param name="token">CancellationToken</param>
    /// <returns>Dictionary of ElasticTranscoder presets by name, keyed by name</returns>
    Task<Dictionary<string, TranscoderPreset>> GetPresetLookupByName(CancellationToken token = default);
    
    /// <summary>
    /// Get a lookup of preset {id}:{preset}
    /// (e.g. "1351620000001-000001": {1351620000001-000001, 'System Preset: Generic 1080p', 'mp4'})
    /// </summary>
    /// <param name="token">CancellationToken</param>
    /// <returns>Dictionary of ElasticTranscoder presets, keyed by id</returns>
    Task<Dictionary<string, TranscoderPreset>> GetPresetLookupById(CancellationToken token = default);
}
