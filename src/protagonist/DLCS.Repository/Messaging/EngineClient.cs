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
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Caching;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DLCS.AWS.SQS.SqsQueueUtilities.Constants.MessageAttributeNames;

namespace DLCS.Repository.Messaging;

/// <summary>
/// A thin wrapper to manage interactions with the Engine - direct and indirect 
/// </summary>
public class EngineClient(
    IQueueLookup queueLookup,
    IQueueSender queueSender,
    HttpClient httpClient,
    IAppCache appCache,
    IOptions<CacheSettings> cacheOptions,
    ILogger<EngineClient> logger)
    : IEngineClient
{
    private readonly CacheSettings cacheSettings = cacheOptions.Value;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly IReadOnlyDictionary<string, TranscoderPreset> NullPresetDictionary =
        new Dictionary<string, TranscoderPreset>();

    public async Task<HttpStatusCode> SynchronousIngest(Asset asset, CancellationToken cancellationToken = default)
    {
        var jsonString = GetJsonString(asset);
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes(jsonString));

        try
        {
            var response = await httpClient.PostAsync("asset-ingest", content, cancellationToken);
            return response.StatusCode;
        }
        catch (WebException ex)
        {
            if (ex is { Status: WebExceptionStatus.ProtocolError, Response: HttpWebResponse response })
            {
                return response.StatusCode;
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
            logger.LogError("Request to ingest {AssetId} cancelled", asset.Id);
        }

        return HttpStatusCode.InternalServerError;
    }
    
    public async Task<bool> AsynchronousIngest(IDeliverable deliverable, CancellationToken cancellationToken = default)
    {
        var queueName = GetQueueName(deliverable); 
        var jsonString = GetJsonString(deliverable);
        var ingestType = GetIngestType(deliverable);
        var attributes = new Dictionary<string, string> { [IngestType] = ingestType };
        var success = await queueSender.QueueMessage(queueName, jsonString, attributes, cancellationToken);

        if (!success)
        {
            logger.LogInformation("Error queueing ingest request for {DeliverableId}", deliverable.Identifier());
        }
        else
        {
            logger.LogDebug("Successfully enqueued ingest request for {DeliverableId}", deliverable.Identifier());
        }

        return success;
    }

    public async Task<int> AsynchronousIngestBatch(IReadOnlyCollection<Asset> assets,
        bool isPriority, CancellationToken cancellationToken)
    {
        var overallSent = 0;
        var batchId = (assets.First().Batch ?? 0).ToString();

        // Get a grouping of items in batch by Family - different families can use different queues 
        var byFamily = assets.GroupBy(a => a.Family);
        
        foreach (var familyGrouping in byFamily)
        {
            logger.LogDebug("Sending '{Family}' notifications for {BatchId}", familyGrouping.Key, batchId);
            
            var queueName = GetQueueName(familyGrouping.First(), isPriority);
            var sentCount =
                await AsynchronousIngestBatch(familyGrouping.ToList(), queueName, cancellationToken);
            overallSent += sentCount;
        }

        return overallSent;
    }

    public Task<int> AsynchronousIngestBatch(IReadOnlyCollection<Adjunct> adjuncts,
        CancellationToken cancellationToken = default)
        => AsynchronousIngestBatch(adjuncts, GetQueueName(adjuncts.First()), cancellationToken);

    private async Task<int> AsynchronousIngestBatch(IReadOnlyCollection<IDeliverable> deliverables, string queueName,
        CancellationToken cancellationToken)
    {
        // Grab the first deliverable to derive some values representative of the whole batch 
        var firstDeliverable = deliverables.First();
        var batchId = (firstDeliverable.Batch ?? 0).ToString();
        var deliverableType = firstDeliverable.GetType().Name;
        
        logger.LogDebug("Sending '{DeliverableType}' notifications for {BatchId}", deliverableType, batchId);
        var capacity = deliverables.Count;

        var jsonStrings = new List<string>(capacity);
        var ingestType = GetIngestType(firstDeliverable);

        foreach (var deliverable in deliverables)
        {
            jsonStrings.Add(GetJsonString(deliverable));
        }

        var attributes = new Dictionary<string, string> { [IngestType] = ingestType };
        var sentCount = await queueSender.QueueMessages(queueName, jsonStrings, batchId, attributes, cancellationToken);
        if (sentCount < capacity)
        {
            logger.LogWarning("Some messages failed to queue for {BatchId}, '{DeliverableType}'", batchId,
                deliverableType);
        }

        return sentCount;
    }

    public async Task<IReadOnlyCollection<string>?> GetAllowedAvPolicyOptions(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("av/allowed", cancellationToken);
            return await response.Content.ReadFromJsonAsync<IReadOnlyCollection<string>>(
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve allowed iiif-av policy options from Engine");
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, TranscoderPreset>> GetAvPresets(
        CancellationToken cancellationToken = default)
    {
        const string key = "avPresetList";
        return await appCache.GetOrAddAsync(key, async entry =>
        {
            try
            {
                var response = await httpClient.GetAsync("av/presets", cancellationToken);
                return await response.Content.ReadFromJsonAsync<IReadOnlyDictionary<string, TranscoderPreset>>(
                    cancellationToken: cancellationToken) ?? new Dictionary<string, TranscoderPreset>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve allowed iiif-av policy options from Engine");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return NullPresetDictionary;
            }
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long));
    }

    private static string GetIngestType(IDeliverable deliverable) => deliverable switch
    {
        Adjunct => IngestAdjunctRequest.IngestType,
        Asset => IngestAssetRequest.IngestType,
        _ => ThrowUnknownType(deliverable)
    };

    private static string GetJsonString(IDeliverable deliverable)
    {
        object request = deliverable switch
        {
            Adjunct adjunct => new IngestAdjunctRequest(adjunct.Id, adjunct.AssetId, DateTime.UtcNow),
            Asset asset => new IngestAssetRequest(asset.Id, DateTime.UtcNow, asset.Batch),
            _ => ThrowUnknownType(deliverable)
        };
        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    private string GetQueueName(IDeliverable? deliverable, bool isPriority = false) =>
        deliverable switch
        {
            Adjunct => queueLookup.GetAdjunctsQueueName(),
            Asset asset => queueLookup.GetQueueNameForFamily(asset.Family ?? new AssetFamily(), isPriority),
            _ => ThrowUnknownType(deliverable)
        };

    private static string ThrowUnknownType(IDeliverable? deliverable) =>
        throw new ArgumentException($"Unknown deliverable type {deliverable?.GetType().Name ?? "unknown"}");
}
