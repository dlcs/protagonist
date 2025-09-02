using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Caching;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeSpan = System.TimeSpan;

namespace DLCS.AWS.ElasticTranscoder;

[Obsolete("ElasticTranscode is being replaced by MediaConvert")]
public class ElasticTranscoderPresetLookup : ITranscoderPresetLookup
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly IAppCache cache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<ElasticTranscoderPresetLookup> logger;

    public ElasticTranscoderPresetLookup(IAmazonElasticTranscoder elasticTranscoder,
        IAppCache cache, 
        IOptions<CacheSettings> cacheSettings,
        ILogger<ElasticTranscoderPresetLookup> logger)
    {
        this.elasticTranscoder = elasticTranscoder;
        this.cache = cache;
        this.cacheSettings = cacheSettings.Value;
        this.logger = logger;
    }

    public Dictionary<string, TranscoderPreset> GetPresetLookupByPolicyName()
        => GetPresetLookup(preset => preset.PolicyName).Result;

    public Dictionary<string, TranscoderPreset> GetPresetLookupById()
        => GetPresetLookup(preset => preset.Id).Result;

    private async Task<Dictionary<string, TranscoderPreset>> GetPresetLookup(Func<TranscoderPreset, string> getKey)
    {
        var presets = await RetrievePresets();

        var presetsDictionary = presets.ToDictionary(getKey, pair => pair);

        return presetsDictionary;
    }

    private Task<List<TranscoderPreset>> RetrievePresets()
    {
        const string presetLookupKey = "MediaTranscode:Presets";
        
        return cache.GetOrAddAsync(presetLookupKey, async entry =>
        {
            var presets = new List<TranscoderPreset>();
            var response = new ListPresetsResponse();
            
            do
            {
                var request = new ListPresetsRequest { PageToken = response.NextPageToken };
                response = await elasticTranscoder.ListPresetsAsync(request);

                presets.AddRange(response.Presets.Select(r => new TranscoderPreset(r.Id, r.Name, r.Container))
                    .ToList());
            } while (response.NextPageToken != null);

            if (presets.Count == 0)
            {
                logger.LogWarning("No ElasticTranscoder presets found");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            }

            return presets;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long, priority: CacheItemPriority.Low));
    }
}
