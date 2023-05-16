﻿using System.Net;
using System.Text.Json;
using DLCS.Model.Messaging;
using Engine.Ingest.Models;
using Microsoft.AspNetCore.Mvc;

namespace Engine.Ingest;

[ApiController]
public class IngestController : Controller
{
    private readonly IAssetIngester ingester;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public IngestController(IAssetIngester ingester)
    {
        this.ingester = ingester;
    }

    /// <summary>
    /// Synchronously ingest an asset using legacy model
    /// </summary>
    [HttpPost]
    [Route("image-ingest")]
    public async Task<IActionResult> IngestImage(CancellationToken cancellationToken)
    {
        // TODO - throw if this is a 'T' request
        var message =
            await JsonSerializer.DeserializeAsync<LegacyIngestEvent>(Request.Body,
                JsonSerializerOptions, cancellationToken);

        // TODO - throw if this is a 'T' request
        var result = await ingester.Ingest(message, cancellationToken);

        return ConvertToStatusCode(message, result.Status);
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