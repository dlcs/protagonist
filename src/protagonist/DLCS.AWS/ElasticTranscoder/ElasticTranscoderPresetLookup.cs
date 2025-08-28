using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Caching;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeSpan = System.TimeSpan;

namespace DLCS.AWS.ElasticTranscoder;

public class ElasticTranscoderPresetLookup : IElasticTranscoderPresetLookup
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

    public Task<Dictionary<string, TranscoderPreset>> GetPresetLookupByName(CancellationToken token = default)
        => GetPresetLookup(preset => preset.Name, token);

    public Task<Dictionary<string, TranscoderPreset>> GetPresetLookupById(CancellationToken token = default)
        => GetPresetLookup(preset => preset.Id, token);

    private async Task<Dictionary<string, TranscoderPreset>> GetPresetLookup(Func<TranscoderPreset, string> getKey,
        CancellationToken token)
    {
        var presets = await RetrievePresets(token);

        var presetsDictionary = presets.ToDictionary(getKey, pair => pair);

        return presetsDictionary;
    }

    private Task<List<TranscoderPreset>> RetrievePresets(CancellationToken token)
    {
        const string presetLookupKey = "MediaTranscode:Presets";
        
        return cache.GetOrAddAsync(presetLookupKey, async entry =>
        {
            var presets = new List<TranscoderPreset>();
            var response = new ListPresetsResponse();
            
            do
            {
                var request = new ListPresetsRequest { PageToken = response.NextPageToken };
                response = await elasticTranscoder.ListPresetsAsync(request, token);

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
