using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Orchestration
{
    public interface IImageOrchestrator
    {
        Task OrchestrateImage(OrchestrationImage orchestrationImage,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Class that contains logic for copying images from slow object-storage to fast-disk storage
    /// </summary>
    public class ImageOrchestrator : IImageOrchestrator
    {
        private readonly IAssetTracker assetTracker;
        private readonly IOptions<OrchestratorSettings> orchestratorSettings;
        private readonly S3AmbientOriginStrategy s3OriginStrategy;
        private readonly FileSaver fileSaver;
        private readonly IDlcsApiClient dlcsApiClient;
        private readonly ILogger<ImageOrchestrator> logger;
        private readonly AsyncKeyedLock asyncLocker = new();

        public ImageOrchestrator(IAssetTracker assetTracker,
            IOptions<OrchestratorSettings> orchestratorSettings,
            S3AmbientOriginStrategy s3OriginStrategy,
            FileSaver fileSaver,
            IDlcsApiClient dlcsApiClient,
            ILogger<ImageOrchestrator> logger)
        {
            this.assetTracker = assetTracker;
            this.orchestratorSettings = orchestratorSettings;
            this.s3OriginStrategy = s3OriginStrategy;
            this.fileSaver = fileSaver;
            this.dlcsApiClient = dlcsApiClient;
            this.logger = logger;
        }
        
        public async Task OrchestrateImage(OrchestrationImage orchestrationImage,
            CancellationToken cancellationToken = default)
        {
            var assetId = orchestrationImage.AssetId;
            if (orchestrationImage.Status == OrchestrationStatus.Orchestrated)
            {
                logger.LogDebug("Asset '{AssetId}' already orchestrated, no-op", assetId);
                return;
            }

            using (var updateLock = await GetLock(assetId, cancellationToken))
            {
                var (saveSuccess, currentOrchestrationImage) =
                    await assetTracker.TrySetOrchestrationStatus(orchestrationImage, OrchestrationStatus.Orchestrating,
                        cancellationToken: cancellationToken);
                
                if (!saveSuccess && currentOrchestrationImage.Status == OrchestrationStatus.Orchestrated)
                {
                    // Previous lock holder has orchestrated image, abort.
                    logger.LogDebug("Asset '{AssetId}' lock attained but image orchestrated",
                        assetId);
                    return;
                }
                
                // TODO - should this be done prior to entering the lock?
                if (string.IsNullOrEmpty(orchestrationImage.S3Location))
                {
                    logger.LogWarning("Asset '{AssetId}' has no s3 location, reingesting", assetId);
                    
                    if (!await dlcsApiClient.ReingestAsset(assetId, cancellationToken))
                    {
                        logger.LogWarning("Error reingesting asset '{AssetId}'", assetId);
                        throw new ApplicationException($"Unable to ingest Asset '{assetId}' from origin");
                    }

                    orchestrationImage = await assetTracker.RefreshCachedAsset<OrchestrationImage>(assetId);
                }

                var targetPath = orchestratorSettings.Value.GetImageLocalPath(assetId, false);
                await SaveImageToFastDisk(orchestrationImage, targetPath, cancellationToken);

                // Save status as Orchestrated
                await assetTracker.TrySetOrchestrationStatus(orchestrationImage, OrchestrationStatus.Orchestrated,
                    true, cancellationToken);
            }
        }

        private async Task SaveImageToFastDisk(OrchestrationImage image, string filePath, CancellationToken cancellationToken)
        {
            // Get bytes from S3
            await using (var originResponse = await s3OriginStrategy.LoadAssetFromOrigin(image.AssetId,
                image.S3Location, null, cancellationToken))
            {
                if (originResponse == null || originResponse.Stream == Stream.Null)
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
        
        private async Task<AsyncKeyedLock.Releaser> GetLock(AssetId assetId, CancellationToken cancellationToken)
        {
            // TODO - change timeout logic to vary on asset size
            var lockKey = ImageOrchestrationKeys.GetOrchestrationLockKey(assetId);
            var lockTimeout = TimeSpan.FromMilliseconds(orchestratorSettings.Value.CriticalPathTimeoutMs);
            var updateLock = await asyncLocker.LockAsync(lockKey, lockTimeout, false, cancellationToken);

            if (!updateLock.HaveLock)
            {
                logger.LogWarning("Unable to attain orchestration lock for {AssetId} within {Timeout}ms",
                    assetId, lockTimeout.TotalMilliseconds);
            }

            return updateLock;
        }
    }

    public static class ImageOrchestrationKeys
    {
        public static string GetOrchestrationLockKey(AssetId assetId) => $"orch:{assetId}";
    }
}