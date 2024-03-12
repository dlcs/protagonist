using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    
    private static readonly JsonSerializerOptions SerializerOptions = new (JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
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

    public async Task<int> AsynchronousIngestBatch(IReadOnlyCollection<IngestAssetRequest> ingestAssetRequests,
        bool isPriority, CancellationToken cancellationToken)
    {
        var overallSent = 0;
        var batchId = (ingestAssetRequests.First().Asset.Batch ?? 0).ToString();
        
        // Get a grouping of items in batch by Family - different families can use different queues 
        var byFamily = ingestAssetRequests.GroupBy(a => a.Asset.Family);
        foreach (var familyGrouping in byFamily)
        {
            logger.LogDebug("Sending '{Family}' notifications for {BatchId}", familyGrouping.Key, batchId);
            var queueName = queueLookup.GetQueueNameForFamily(familyGrouping.Key ?? new AssetFamily(), isPriority);
            var capacity = familyGrouping.Count();
            
            var jsonStrings = new List<string>(capacity);
            foreach (IngestAssetRequest iar in familyGrouping)
            {
                jsonStrings.Add(await GetJsonString(iar, true));
            }

            var sentCount = await queueSender.QueueMessages(queueName, jsonStrings, batchId, cancellationToken);
            overallSent += sentCount;
            if (sentCount < capacity)
            {
                logger.LogWarning("Some messages failed to queue for {BatchId}, family {Family}", batchId,
                    familyGrouping.Key);
            }
        }

        return overallSent;
    }

    public async Task<IReadOnlyCollection<string>?> GetAllowedAvPolicyOptions(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("allowed-av", cancellationToken);
            return await response.Content.ReadFromJsonAsync<IReadOnlyCollection<string>>(
                cancellationToken: cancellationToken);
            
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve allowed iiif-av policy options from Engine");
            return null;
        }
    }
    
    private async Task<string> GetJsonString(IngestAssetRequest ingestAssetRequest, bool derivativesOnly)
    {
        // If running in legacy mode, the payload should contain the full Legacy JSON string
        if (dlcsSettings.UseLegacyEngineMessage)
        {
            var legacyJson = await LegacyJsonMessageHelpers.GetLegacyJsonString(ingestAssetRequest, derivativesOnly);
            return legacyJson;
        }
        else
        {
            // Otherwise, it should contain only the Asset ID - for now, this is an Asset object containing just the ID
            var jsonString = JsonSerializer.Serialize(GetMinimalIngestAssetRequest(ingestAssetRequest), SerializerOptions);
            return jsonString;
        }
    }

    private IngestAssetRequest GetMinimalIngestAssetRequest(IngestAssetRequest ingestAssetRequest)
    {
        return new IngestAssetRequest(new Asset(){ Id = ingestAssetRequest.Asset.Id }, ingestAssetRequest.Created);
    }
}

