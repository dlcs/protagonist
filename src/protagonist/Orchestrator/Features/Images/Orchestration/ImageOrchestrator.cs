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
using Orchestrator.Infrastructure.Deliverator;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Orchestration;

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
    private readonly IOptionsMonitor<OrchestratorSettings> orchestratorSettings;
    private readonly S3AmbientOriginStrategy s3OriginStrategy;
    private readonly IAppCache appCache;
    private readonly IFileSaver fileSaver;
    private readonly IFileSystem fileSystem;
    private readonly IDlcsApiClient dlcsApiClient;
    private readonly ILogger<ImageOrchestrator> logger;

    public ImageOrchestrator(IAssetTracker assetTracker,
        IOptionsMonitor<OrchestratorSettings> orchestratorSettings,
        S3AmbientOriginStrategy s3OriginStrategy,
        IAppCache appCache,
        IFileSaver fileSaver,
        IFileSystem fileSystem,
        IDlcsApiClient dlcsApiClient,
        ILogger<ImageOrchestrator> logger)
    {
        this.assetTracker = assetTracker;
        this.orchestratorSettings = orchestratorSettings;
        this.s3OriginStrategy = s3OriginStrategy;
        this.appCache = appCache;
        this.fileSaver = fileSaver;
        this.fileSystem = fileSystem;
        this.dlcsApiClient = dlcsApiClient;
        this.logger = logger;
    }
    
    public async Task OrchestrateImage(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default)
    {
        var assetId = orchestrationImage.AssetId;

        await appCache.GetOrAddAsync(CacheKeys.GetOrchestrationCacheKey(assetId), async _ =>
        {
            await OrchestrateImage(orchestrationImage, cancellationToken, assetId);
            // TODO - catch exceptions and cache a short lived value??
            return true;
        }, orchestratorSettings.CurrentValue.Caching.GetMemoryCacheOptions(priority: CacheItemPriority.High));
    }

    private async Task OrchestrateImage(OrchestrationImage? orchestrationImage, CancellationToken cancellationToken,
        AssetId assetId)
    {
        logger.LogDebug("Populating orchestration cache for '{AssetId}'", assetId);

        var targetPath = orchestratorSettings.CurrentValue.GetImageLocalPath(assetId);
        if (DoesFileForAssetExist(targetPath))
        {
            logger.LogDebug("File for '{AssetId}' already on disk. no-op", assetId);
            return;
        }

        if (string.IsNullOrEmpty(orchestrationImage?.S3Location))
        {
            orchestrationImage = await ReingestImage(cancellationToken, assetId);
        }
        
        await SaveImageToFastDisk(orchestrationImage, targetPath, cancellationToken);
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

    private async Task<OrchestrationImage> ReingestImage(CancellationToken cancellationToken, AssetId assetId)
    {
        logger.LogInformation("Asset '{AssetId}' has no s3 location, reingesting", assetId);

        if (!await dlcsApiClient.ReingestAsset(assetId, cancellationToken))
        {
            logger.LogWarning("Error reingesting asset '{AssetId}'", assetId);
            throw new ApplicationException($"Unable to reingest Asset '{assetId}' from origin");
        }

        // TODO - does this want to be a 'get' with an extra param to refresh?
        var orchestrationImage = await assetTracker.RefreshCachedAsset<OrchestrationImage>(assetId);

        if (orchestrationImage == null)
        {
            logger.LogWarning("Error refreshing asset '{AssetId}'", assetId);
            throw new ApplicationException($"Error refreshing cached Asset '{assetId}'");
        }

        return orchestrationImage;
    }

    private async Task SaveImageToFastDisk(OrchestrationImage image, string filePath, CancellationToken cancellationToken)
    {
        // Get bytes from S3
        await using var originResponse =
            await s3OriginStrategy.LoadAssetFromOrigin(image.AssetId, image.S3Location, null, cancellationToken);
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