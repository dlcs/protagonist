using DLCS.AWS.Settings;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Caching;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Transcoding;

/// <summary>
/// Implementation of <see cref="ITranscoderPresetLookup"/> that uses preconfigured options only for generating the
/// transcoding preset lookups
/// </summary>
public class SettingsBasedPresetLookup(
    IAppCache cache,
    IOptionsMonitor<AWSSettings> awsOptions,
    IOptionsMonitor<CacheSettings> cacheSettings,
    ILogger<SettingsBasedPresetLookup> logger)
    : ITranscoderPresetLookup
{
    /// <inheritdoc />
    public Dictionary<string, TranscoderPreset> GetPresetLookupByPolicyName()
        => GetPresetLookup(preset => preset.PolicyName);

    /// <inheritdoc />
    public Dictionary<string, TranscoderPreset> GetPresetLookupById()
        => GetPresetLookup(preset => preset.Id);

    private Dictionary<string, TranscoderPreset> GetPresetLookup(Func<TranscoderPreset, string> getKey)
    {
        var presets = RetrievePresets();

        var presetsDictionary = presets.ToDictionary(getKey, pair => pair);

        return presetsDictionary;
    }

    private List<TranscoderPreset> RetrievePresets()
    {
        const string presetLookupKey = "MediaTranscode:Presets";

        return cache.GetOrAdd(presetLookupKey, entry =>
        {
            var mappings = awsOptions.CurrentValue.Transcode.DeliveryChannelMappings;
            var presets = mappings.Select(kvp =>
            {
                var valueParts = kvp.Value.Split("|");
                if (valueParts.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"'{kvp.Value}' is an invalid transcode preset format. Must be '{{preset}}|{{format}}'");
                }

                return new TranscoderPreset(valueParts[0], kvp.Key, valueParts[1]);
            }).ToList();
            
            if (presets.Count == 0)
            {
                logger.LogWarning("No Transcode presets found");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.CurrentValue.GetTtl(CacheDuration.Short));
            }
            
            return presets;
        }, cacheSettings.CurrentValue.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.Low));
    }
}
