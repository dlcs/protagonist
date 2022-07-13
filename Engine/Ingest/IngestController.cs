using DLCS.Model.Messaging;
using Engine.Ingest.Models;
using Microsoft.AspNetCore.Mvc;

namespace Engine.Ingest;

[ApiController]
public class IngestController : Controller
{
    private readonly IAssetIngester ingester;

    public IngestController(IAssetIngester ingester)
    {
        this.ingester = ingester;
    }
        
    /// <summary>
    /// Synchronously ingest an asset using legacy model
    /// </summary>
    [HttpPost]
    [Route("image-ingest")]
    public async Task<IActionResult> IngestImage([FromBody] LegacyIngestEvent message, 
        CancellationToken cancellationToken)
    {
        // TODO - throw if this is a 'T' request
        var result = await ingester.Ingest(message, cancellationToken);

        return ConvertToStatusCode(message, result);
    }
    
    /// <summary>
    /// Synchronously ingest an asset 
    /// </summary>
    [HttpPost]
    [Route("asset-ingest")]
    public async Task<IActionResult> IngestAsset([FromBody] IngestAssetRequest message, 
        CancellationToken cancellationToken)
    {
        // TODO - throw if this is a 'T' request
        var result = await ingester.Ingest(message, cancellationToken);

        return ConvertToStatusCode(message, result);
    }

    private IActionResult ConvertToStatusCode(object message, IngestResult result)
        => result switch
        {
            IngestResult.Failed => StatusCode(500, message),
            IngestResult.Success => Ok(message),
            IngestResult.QueuedForProcessing => Accepted(message),
            IngestResult.Unknown => StatusCode(500, message),
            _ => StatusCode(500, message)
        };
}