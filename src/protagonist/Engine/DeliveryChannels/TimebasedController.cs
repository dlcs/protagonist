using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using Microsoft.AspNetCore.Mvc;

namespace Engine.DeliveryChannels;

/// <summary>
/// Controller for API requests related to AV presets and delivery-channel mappings.
/// Engine is the source of truth for this information, these endpoints allow other services to request config
/// </summary>
[ApiController]
[Route("av")]
public class TimebasedController(
    ITranscoderPresetLookup transcoderPresetLookup)
    : Controller
{
    /// <summary>
    /// Retrieve allowed av 'friendly' preset names
    /// </summary>
    [HttpGet]
    [Route("allowed")]
    public List<string> GetAllowedAvOptions() => transcoderPresetLookup.GetPresetLookupByPolicyName().Keys.ToList();

    /// <summary>
    /// Retrieve av option presets
    /// </summary>
    [HttpGet]
    [Route("presets")]
    public Dictionary<string, TranscoderPreset> GetKnownPresets() =>
        transcoderPresetLookup.GetPresetLookupByPolicyName();
}
