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
    
    public async Task<HttpStatusCode> SynchronousIngest(Asset asset, CancellationToken cancellationToken = default)
    {
        var jsonString = GetJsonString(asset);
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
            logger.LogError("Request to ingest {AssetId} cancelled", ingestAssetRequest.Id);
        }

        return HttpStatusCode.InternalServerError;
    }

    public async Task<bool> AsynchronousIngest(Asset asset,
        CancellationToken cancellationToken = default)
    {
        var queueName = queueLookup.GetQueueNameForFamily(asset.Family ?? new AssetFamily());
        var ingestAssetRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow);
        
        var jsonString = GetJsonString(asset);
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

    public async Task<int> AsynchronousIngestBatch(IReadOnlyCollection<Asset> assets,
        bool isPriority, CancellationToken cancellationToken)
    {
        var overallSent = 0;
        var batchId = (assets.First().Batch ?? 0).ToString();
        
        // Get a grouping of items in batch by Family - different families can use different queues 
        var byFamily  = assets.GroupBy(a => a.Family);
        
        foreach (var familyGrouping in byFamily)
        {
            logger.LogDebug("Sending '{Family}' notifications for {BatchId}", familyGrouping.Key, batchId);
            var queueName = queueLookup.GetQueueNameForFamily(familyGrouping.Key ?? new AssetFamily(), isPriority);
            var capacity = familyGrouping.Count();
            
            var jsonStrings = new List<string>(capacity);
            foreach (var asset in familyGrouping.Select(a => a))
            {
                var ingestAssetRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow);
                jsonStrings.Add(GetJsonString(asset));
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
    
    private string GetJsonString(Asset asset)
    {
        // If running in legacy mode, the payload should contain the full Legacy JSON string
        if (dlcsSettings.UseLegacyEngineMessage)
        {
            throw new InvalidOperationException("Legacy ingest events are no longer supported");
        }
        
        var ingestAssetRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow);

        // Otherwise, it should contain only the Asset ID - for now, this is an Asset object containing just the ID
        var jsonString = JsonSerializer.Serialize(ingestAssetRequest, SerializerOptions);
        return jsonString;
    }
}

