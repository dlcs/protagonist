using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Core.Streams;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Deliverator;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Orchestration;

public interface IImageOrchestrator
{
    Task OrchestrateImage(OrchestrationImage orchestrationImage, CancellationToken cancellationToken = default);
}

/// <summary>
/// Class that contains logic for copying images from slow object-storage to fast-disk storage
/// </summary>
public class ImageOrchestrator : IImageOrchestrator
{
    private readonly IAssetTracker assetTracker;
    private readonly IOptions<OrchestratorSettings> orchestratorSettings;
    private readonly S3AmbientOriginStrategy s3OriginStrategy;
    private readonly IFileSaver fileSaver;
    private readonly IDlcsApiClient dlcsApiClient;
    private readonly ILogger<ImageOrchestrator> logger;
    private readonly OrchestrationLock orchestrationLocker;

    public ImageOrchestrator(IAssetTracker assetTracker,
        IOptions<OrchestratorSettings> orchestratorSettings,
        S3AmbientOriginStrategy s3OriginStrategy,
        IFileSaver fileSaver,
        IDlcsApiClient dlcsApiClient,
        OrchestrationLock orchestrationLocker,
        ILogger<ImageOrchestrator> logger)
    {
        this.assetTracker = assetTracker;
        this.orchestratorSettings = orchestratorSettings;
        this.s3OriginStrategy = s3OriginStrategy;
        this.fileSaver = fileSaver;
        this.dlcsApiClient = dlcsApiClient;
        this.orchestrationLocker = orchestrationLocker;
        this.logger = logger;
    }
    
    public async Task OrchestrateImage(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default)
    {
        OrchestrationImage? workingImage = orchestrationImage;
        var assetId = workingImage.AssetId;
        if (workingImage.Status == OrchestrationStatus.Orchestrated)
        {
            logger.LogDebug("Asset '{AssetId}' already orchestrated, no-op", assetId);
            return;
        }
        
        using (var updateLock = await GetLock(assetId, cancellationToken))
        {
            if (workingImage.Status == OrchestrationStatus.Unknown)
            {
                logger.LogDebug("OrchestrationStatus unknown for '{AssetId}', refreshing..", assetId);
                workingImage = await assetTracker.RefreshCachedAsset<OrchestrationImage>(assetId, true);
                workingImage.ThrowIfNull(nameof(workingImage));
            }
            
            var (saveSuccess, currentOrchestrationImage) =
                await assetTracker.TrySetOrchestrationStatus(workingImage, OrchestrationStatus.Orchestrating,
                    cancellationToken: cancellationToken);
            
            if (!saveSuccess && currentOrchestrationImage.Status == OrchestrationStatus.Orchestrated)
            {
                // Previous lock holder has orchestrated image, abort.
                logger.LogDebug("Asset '{AssetId}' lock attained but image orchestrated",
                    assetId);
                return;
            }
            
            // TODO - should this be done prior to entering the lock?
            if (string.IsNullOrEmpty(workingImage.S3Location))
            {
                logger.LogInformation("Asset '{AssetId}' has no s3 location, reingesting", assetId);
                
                if (!await dlcsApiClient.ReingestAsset(assetId, cancellationToken))
                {
                    logger.LogWarning("Error reingesting asset '{AssetId}'", assetId);
                    throw new ApplicationException($"Unable to ingest Asset '{assetId}' from origin");
                }

                workingImage = await assetTracker.RefreshCachedAsset<OrchestrationImage>(assetId, true);
                workingImage.ThrowIfNull(nameof(workingImage));
            }

            var targetPath = orchestratorSettings.Value.GetImageLocalPath(assetId);
            await SaveImageToFastDisk(workingImage, targetPath, cancellationToken);

            // Save status as Orchestrated
            await assetTracker.TrySetOrchestrationStatus(workingImage, OrchestrationStatus.Orchestrated,
                true, cancellationToken);
        }
    }

    private async Task SaveImageToFastDisk(OrchestrationImage image, string filePath, CancellationToken cancellationToken)
    {
        // Get bytes from S3
        await using (var originResponse = await s3OriginStrategy.LoadAssetFromOrigin(image.AssetId,
            image.S3Location, null, cancellationToken))
        {
            if (originResponse == null || originResponse.Stream.IsNull())
            {
                // TODO correct type of exception? Custom type?
                logger.LogWarning("Unable to get asset {Asset} from {Origin}", image.AssetId,
                    image.S3Location);
                throw new ApplicationException($"Unable to get asset '{image.AssetId}' from origin");
            }
            
            // Save bytes to disk
            await fileSaver.SaveResponseToDisk(image.AssetId, originResponse, filePath, cancellationToken);
        }
    }
    
    private async Task<ILock> GetLock(AssetId assetId, CancellationToken cancellationToken)
    {
        // TODO - change timeout logic to vary on asset size?
        var lockTimeout = TimeSpan.FromMilliseconds(orchestratorSettings.Value.CriticalPathTimeoutMs);
        var updateLock = await orchestrationLocker.LockAsync(assetId, lockTimeout, false, cancellationToken);

        if (!updateLock.ExclusiveLock)
        {
            logger.LogWarning("Unable to attain orchestration lock for {AssetId} within {Timeout}ms",
                assetId, lockTimeout.TotalMilliseconds);
        }

        return updateLock;
    }
}