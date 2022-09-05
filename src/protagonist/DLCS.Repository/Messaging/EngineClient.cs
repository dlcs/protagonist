﻿using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.SQS;
using DLCS.Core.Settings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Messaging;

/// <summary>
/// A thin wrapper to manage interactions with the Engine - direct and indirect 
/// </summary>
public class EngineClient : IEngineClient
{
    private readonly IQueueLookup queueLookup;
    private readonly IQueueSender queueSender;
    private readonly HttpClient httpClient;
    private readonly ILogger<EngineClient> logger;
    private readonly DlcsSettings dlcsSettings;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public EngineClient(
        IQueueLookup queueLookup,
        IQueueSender queueSender,
        HttpClient httpClient,
        IOptions<DlcsSettings> dlcsSettings,
        ILogger<EngineClient> logger)
    {
        this.queueLookup = queueLookup;
        this.queueSender = queueSender;
        this.httpClient = httpClient;
        this.logger = logger;
        this.dlcsSettings = dlcsSettings.Value;
    }
    
    public async Task<HttpStatusCode> SynchronousIngest(IngestAssetRequest ingestAssetRequest, 
        bool derivativesOnly = false, CancellationToken cancellationToken = default)
    {
        var jsonString = await GetJsonString(ingestAssetRequest, derivativesOnly);
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes(jsonString));

        try
        {
            var response = await httpClient.PostAsync(dlcsSettings.EngineDirectIngestUri, content, cancellationToken);
            return response.StatusCode;
        }
        catch (WebException ex)
        {
            if (ex.Status == WebExceptionStatus.ProtocolError)
            {
                if (ex.Response is HttpWebResponse response)
                {
                    return response.StatusCode;
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            if (httpEx.StatusCode.HasValue)
            {
                return httpEx.StatusCode.Value;
            }
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Request to ingest {AssetId} cancelled", ingestAssetRequest.Asset.Id);
        }

        return HttpStatusCode.InternalServerError;
    }

    public async Task<bool> AsynchronousIngest(IngestAssetRequest ingestAssetRequest, 
        CancellationToken cancellationToken = default)
    {
        var queueName = queueLookup.GetQueueNameForFamily(ingestAssetRequest.Asset.Family ?? new AssetFamily());
        var jsonString = await GetJsonString(ingestAssetRequest, false);

        var success = await queueSender.QueueMessage(queueName, jsonString, cancellationToken);

        if (!success)
        {
            logger.LogInformation("Error queueing ingest request {IngestRequest}", ingestAssetRequest);
        }
        else
        {
            logger.LogDebug("Successfully enqueued ingest request {IngestRequest}", ingestAssetRequest);
        }

        return success;
    }
    
    private async Task<string> GetJsonString(IngestAssetRequest ingestAssetRequest, bool derivativesOnly)
    {
        if (dlcsSettings.UseLegacyEngineMessage)
        {
            var legacyJson = await LegacyJsonMessageHelpers.GetLegacyJsonString(ingestAssetRequest, derivativesOnly);
            return legacyJson;
        }
        else
        {
            var jsonString = JsonSerializer.Serialize(ingestAssetRequest, SerializerOptions);
            return jsonString;
        }
    }
}