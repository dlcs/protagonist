using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.Transcoding.Models;
using Engine.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Engine.DeliveryChannels;

/// <summary>
/// Controller for API requests related to AV presets and delivery-channel mappings.
/// Engine is the source of truth for this information, these endpoints allow other services to request config
/// </summary>
[ApiController]
[Route("av")]
public class TimebasedController(
    IElasticTranscoderPresetLookup elasticTranscoderPresetLookup,
    IOptions<EngineSettings> engineSettings)
    : Controller
{
    private readonly Dictionary<string, string> deliveryChannelMappings =
        engineSettings.Value.TimebasedIngest!.DeliveryChannelMappings;

    /// <summary>
    /// Retrieve allowed av 'friendly' preset names
    /// </summary>
    [HttpGet]
    [Route("allowed")]
    public List<string> GetAllowedAvOptions() => deliveryChannelMappings.Keys.ToList();

    /// <summary>
    /// Retrieve av option presets
    /// </summary>
    [HttpGet]
    [Route("presets")]
    public async Task<Dictionary<string, TranscoderPreset>> GetKnownPresets()
    {
        var presets = await elasticTranscoderPresetLookup.GetPresetLookupByName();

        var allowedPresets = presets
            .Where(kvp => deliveryChannelMappings.ContainsValue(kvp.Key))
            .ToDictionary(
                key => deliveryChannelMappings.First(mapping => mapping.Value == key.Key).Key,
                value => value.Value);
        
        return allowedPresets;
    }
}
