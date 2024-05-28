using System.Net;
using System.Text.Json;
using DLCS.AWS.ElasticTranscoder;
using DLCS.Model.Messaging;
using Engine.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Engine.Ingest;

[ApiController]
public class IngestController : Controller
{
    private readonly IAssetIngester ingester;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private TimebasedIngestSettings timebasedIngestSettings;

    public IngestController(IAssetIngester ingester, IElasticTranscoderWrapper elasticTranscoderWrapper, 
        IOptions<EngineSettings> engineSettings)
    {
        this.ingester = ingester;
        this.elasticTranscoderWrapper = elasticTranscoderWrapper;
        timebasedIngestSettings = engineSettings.Value.TimebasedIngest;
    }
    
    /// <summary>
    /// Synchronously ingest an asset 
    /// </summary>
    [HttpPost]
    [Route("asset-ingest")]
    public async Task<IActionResult> IngestAsset(CancellationToken cancellationToken)
    {
        // TODO - throw if this is a 'T' request
        var message =
            await JsonSerializer.DeserializeAsync<IngestAssetRequest>(Request.Body,
                JsonSerializerOptions, cancellationToken);
        
        var result = await ingester.Ingest(message, cancellationToken);

        return ConvertToStatusCode(message, result.Status);
    }
    
    /// <summary>
    /// Retrieve allowed av options
    /// </summary>
    [HttpGet]
    [Route("allowed-av")]
    public IActionResult GetAllowedAvOptions()
    {
        return Ok(timebasedIngestSettings.DeliveryChannelMappings.Keys.ToList());
    }
    
    /// <summary>
    /// Retrieve av option presets
    /// </summary>
    [HttpGet]
    [Route("av-presets")]
    public async Task<IActionResult> GetAllowedAvPresetOptions()
    {
        var presets = await elasticTranscoderWrapper.GetPresetIdLookup();

        var allowedPresets =
            presets.Where(x => timebasedIngestSettings.DeliveryChannelMappings.Values.Contains(x.Key))
                .ToDictionary(
                    pair => timebasedIngestSettings.DeliveryChannelMappings.First(x => x.Value == pair.Key)
                        .Key, pair => pair.Value);
        
        return Ok(allowedPresets);
    }

    private IActionResult ConvertToStatusCode(object message, IngestResultStatus result)
        => result switch
        {
            IngestResultStatus.Failed => StatusCode(500, message),
            IngestResultStatus.Success => Ok(message),
            IngestResultStatus.QueuedForProcessing => Accepted(message),
            IngestResultStatus.StorageLimitExceeded => StatusCode((int)HttpStatusCode.InsufficientStorage, message),
            IngestResultStatus.Unknown => StatusCode(500, message),
            _ => StatusCode(500, message)
        };
}