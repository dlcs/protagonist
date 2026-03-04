using System.Net;
using System.Text.Json;
using DLCS.Model.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Engine.Ingest;

[ApiController]
public class IngestController(IAssetIngester assetIngester, IAdjunctIngester adjunctIngester) : Controller
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Synchronously ingest an asset 
    /// </summary>
    [HttpPost]
    [Route("asset-ingest")]
    public async Task<IActionResult> IngestAsset(CancellationToken cancellationToken)
    {
        var message =
            await JsonSerializer.DeserializeAsync<IngestAssetRequest>(Request.Body,
                JsonSerializerOptions, cancellationToken);

        var result = await assetIngester.Ingest(message!, cancellationToken);

        return ConvertToStatusCode(message!, result.Status);
    }
    
    /// <summary>
    /// Synchronously ingest an adjunct 
    /// </summary>
    /// <remarks>Added for easier integration testing without SQS</remarks>
    [HttpPost]
    [Route("adjunct-ingest")]
    public async Task<IActionResult> IngestAdjunct(CancellationToken cancellationToken)
    {
        var message =
            await JsonSerializer.DeserializeAsync<IngestAdjunctRequest>(Request.Body,
                JsonSerializerOptions, cancellationToken);

        var result = await adjunctIngester.Ingest(message!, cancellationToken);

        return ConvertToStatusCode(message!, result.Status);
    }
    
    private ObjectResult ConvertToStatusCode(object message, IngestResultStatus result)
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
