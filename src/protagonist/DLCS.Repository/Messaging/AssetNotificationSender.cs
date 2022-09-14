using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Messaging;

public class AssetNotificationSender : IAssetNotificationSender
{
    private readonly ILogger<AssetNotificationSender> logger;
    private readonly IEngineClient engineClient;

    public AssetNotificationSender(
        IEngineClient engineClient,
        ILogger<AssetNotificationSender> logger)
    {
        this.engineClient = engineClient;
        this.logger = logger;
    }
    
    public async Task<bool> SendIngestAssetRequest(Asset assetToIngest, CancellationToken cancellationToken = default)
    {
        // TODO - increment queue count
        var ingestAssetRequest = new IngestAssetRequest(assetToIngest, DateTime.UtcNow);
        var success = await engineClient.AsynchronousIngest(ingestAssetRequest, cancellationToken);
        return success;
    }
    
    public async Task<HttpStatusCode> SendImmediateIngestAssetRequest(Asset assetToIngest, bool derivativesOnly, CancellationToken cancellationToken = default)
    {
        var ingestAssetRequest = new IngestAssetRequest(assetToIngest, DateTime.UtcNow);
        var statusCode = await engineClient.SynchronousIngest(ingestAssetRequest, derivativesOnly, cancellationToken);
        return statusCode;
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
                logger.LogDebug("Message Bus: Asset Modified: {AssetId}", after?.Id ?? before.Id);
                break;
        }
        
        return Task.CompletedTask;;
    }
}