using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.FileSystem;
using DLCS.Core.Streams;
using DLCS.Core.Types;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.API;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Orchestration;

public interface IImageOrchestrator
{
    Task<OrchestrationResult> EnsureImageOrchestrated(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default);
}

public enum OrchestrationResult
{
    /// <summary>
    /// Asset was already orchestrated to fask-disk
    /// </summary>
    AlreadyOrchestrated,
    
    /// <summary>
    /// Asset was moved from slow storage -> fast-disk
    /// </summary>
    Orchestrated,
    
    /// <summary>
    /// Asset could not be found for serving. E.g. could be a new asset queued for ingestion but not yet complete
    /// </summary>
    NotFound,
    
    /// <summary>
    /// An error was encountered orchestrating asset
    /// </summary>
    Error,
}

/// <summary>
/// Class that contains logic for copying images from slow object-storage to fast-disk storage
/// </summary>
public class ImageOrchestrator : IImageOrchestrator
{
    private readonly IAssetTracker assetTracker;
    private readonly IOptionsMonitor<OrchestratorSettings> orchestratorSettings;
    private readonly IOriginStrategy originStrategy;
    private readonly IAppCache appCache;
    private readonly IFileSaver fileSaver;
    private readonly IFileSystem fileSystem;
    private readonly IDlcsApiClient dlcsApiClient;
    private readonly ILogger<ImageOrchestrator> logger;

    public ImageOrchestrator(IAssetTracker assetTracker,
        IOptionsMonitor<OrchestratorSettings> orchestratorSettings,
        IOriginStrategy originStrategy,
        IAppCache appCache,
        IFileSaver fileSaver,
        IFileSystem fileSystem,
        IDlcsApiClient dlcsApiClient,
        ILogger<ImageOrchestrator> logger)
    {
        this.assetTracker = assetTracker;
        this.orchestratorSettings = orchestratorSettings;
        this.originStrategy = originStrategy;
        this.appCache = appCache;
        this.fileSaver = fileSaver;
        this.fileSystem = fileSystem;
        this.dlcsApiClient = dlcsApiClient;
        this.logger = logger;
    }

    public async Task<OrchestrationResult> EnsureImageOrchestrated(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default)
    {
        var assetId = orchestrationImage.AssetId;

        var orchestrationResult = OrchestrationResult.AlreadyOrchestrated;

        await appCache.GetOrAddAsync(CacheKeys.GetOrchestrationCacheKey(assetId), async _ =>
        {
            try
            {
                orchestrationResult = await OrchestrateImageInternal(orchestrationImage, assetId, cancellationToken);
            }
            catch (Exception)
            {
                orchestrationResult = OrchestrationResult.Error;
            }

            return true;
        }, orchestratorSettings.CurrentValue.Caching.GetMemoryCacheOptions(
            duration: orchestrationResult is OrchestrationResult.Error or OrchestrationResult.NotFound
                ? CacheDuration.Short
                : CacheDuration.Default,
            priority: orchestrationResult is OrchestrationResult.Error or OrchestrationResult.NotFound
                ? CacheItemPriority.Low
                : CacheItemPriority.High));

        return orchestrationResult;
    }

    private async Task<OrchestrationResult> OrchestrateImageInternal(OrchestrationImage orchestrationImage, AssetId assetId, 
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Populating orchestration cache for '{AssetId}'", assetId);

        var targetPath = orchestratorSettings.CurrentValue.GetImageLocalPath(assetId);
        if (DoesFileForAssetExist(targetPath))
        {
            logger.LogTrace("File for '{AssetId}' already on disk. no-op", assetId);
            return OrchestrationResult.AlreadyOrchestrated;
        }
        
        if (orchestrationImage.Reingest)
        {
            orchestrationImage = await ReingestImage(assetId, cancellationToken);
        }

        if (string.IsNullOrEmpty(orchestrationImage.S3Location))
        {
             return OrchestrationResult.NotFound;
        }
        
        await SaveImageToFastDisk(orchestrationImage, targetPath, cancellationToken);
        return OrchestrationResult.Orchestrated;
    }
    
    private bool DoesFileForAssetExist(string targetPath)
    {
        if (fileSystem.FileExists(targetPath))
        {
            fileSystem.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow);
            return true;
        }

        return false;
    }

    private async Task<OrchestrationImage> ReingestImage(AssetId assetId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Asset '{AssetId}' has no s3 location, reingesting", assetId);

        if (!await dlcsApiClient.ReingestAsset(assetId, cancellationToken))
        {
            logger.LogWarning("Error reingesting asset '{AssetId}'", assetId);
            throw new ApplicationException($"Unable to reingest Asset '{assetId}' from origin");
        }

        var orchestrationImage = await assetTracker.RefreshCachedAsset<OrchestrationImage>(assetId);

        if (orchestrationImage == null)
        {
            logger.LogWarning("Error refreshing asset '{AssetId}'", assetId);
            throw new ApplicationException($"Error refreshing cached Asset '{assetId}'");
        }

        return orchestrationImage;
    }

    private async Task SaveImageToFastDisk(OrchestrationImage image, string filePath,
        CancellationToken cancellationToken)
    {
        // Get bytes from origin (S3)
        await using var originResponse =
            await originStrategy.LoadAssetFromOrigin(image.AssetId, image.S3Location, null, cancellationToken);
        if (originResponse == null || originResponse.Stream.IsNull())
        {
            // TODO correct type of exception? Custom type?
            logger.LogWarning("Unable to get asset {Asset} from {Origin}", image.AssetId, image.S3Location);
            throw new ApplicationException($"Unable to get asset '{image.AssetId}' from origin");
        }

        // Save bytes to disk
        await fileSaver.SaveResponseToDisk(image.AssetId, originResponse, filePath, cancellationToken);
    }
}