using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DLCS.AWS.SQS;
using DLCS.Core.Settings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Messaging;

public class AssetNotificationSender : IAssetNotificationSender
{
    private readonly ILogger<AssetNotificationSender> logger;
    private readonly DlcsSettings settings;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IQueueLookup queueLookup;
    private readonly IQueueSender queueSender;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public AssetNotificationSender(
        IHttpClientFactory httpClientFactory,
        IQueueLookup queueLookup,
        IQueueSender queueSender,
        IOptions<DlcsSettings> dlcsSettings,
        ILogger<AssetNotificationSender> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.queueLookup = queueLookup;
        this.queueSender = queueSender;
        this.settings = dlcsSettings.Value;
        this.logger = logger;
    }
    
    public async Task SendIngestAssetRequest(IngestAssetRequest ingestAssetRequest)
    {
        var queueName = queueLookup.GetQueueNameForFamily(ingestAssetRequest.Asset.Family ?? new AssetFamily());
        var jsonString = await GetJsonString(ingestAssetRequest, false);

        var success = await queueSender.QueueMessage(queueName, jsonString);

        if (!success)
        {
            logger.LogInformation("Error queueing ingest request {IngestRequest}", ingestAssetRequest);
        }
    }

    /// <summary>
    /// This currently produces the legacy JSON body that Deliverator Engine expects.
    ///
    /// A much simpler implementation simply sends ingestAssetRequest to Engine directly.
    /// It still needs to be synchronous - callers need the result from Engine.
    /// </summary>
    /// <param name="ingestAssetRequest"></param>
    /// <param name="derivativesOnly"></param>
    /// <returns></returns>
    public async Task<HttpStatusCode> SendImmediateIngestAssetRequest(IngestAssetRequest ingestAssetRequest, bool derivativesOnly)
    {
        var jsonString = await GetJsonString(ingestAssetRequest, derivativesOnly);
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes(jsonString));
        
        try
        {
            using var httpClient = httpClientFactory.CreateClient("engine");
            var response = await httpClient.PostAsync(settings.EngineDirectIngestUri, content);
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

        return HttpStatusCode.InternalServerError;
    }

    public Task SendAssetModifiedNotification(ChangeType changeType, Asset? before, Asset? after)
    {
        switch (changeType)
        {
            case ChangeType.Create when before != null:
                throw new ArgumentException("Asset Creation cannot have a before asset", nameof(before));
            case ChangeType.Create when after == null:
                throw new ArgumentException("Asset Creation must have an after asset", nameof(after));
            case ChangeType.Update when before == null:
                throw new ArgumentException("Asset Update must have a before asset", nameof(before));
            case ChangeType.Update when after == null:
                throw new ArgumentException("Asset Update must have an after asset", nameof(after));
            case ChangeType.Delete when before == null:
                throw new ArgumentException("Asset Delete must have a before asset", nameof(before));
            case ChangeType.Delete when after != null:
                throw new ArgumentException("Asset Delete cannot have an after asset", nameof(after));
            default:
                logger.LogDebug("Message Bus: Asset Modified: {AssetId}", after.Id);
                break;
        }
        
        return Task.CompletedTask;;
    }
    
    private async Task<string> GetJsonString(IngestAssetRequest ingestAssetRequest, bool derivativesOnly)
    {
        if (settings.UseLegacyEngineMessage)
        {
            var legacyJson = await LegacyJsonMessageHelpers.GetLegacyJsonString(ingestAssetRequest, derivativesOnly);
            return legacyJson;
        }
        else
        {
            var jsonString = JsonSerializer.Serialize(ingestAssetRequest, JsonSerializerOptions);
            return jsonString;
        }
    }
}