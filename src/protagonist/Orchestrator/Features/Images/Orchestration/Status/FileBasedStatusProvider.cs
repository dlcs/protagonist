using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Orchestration.Status;

/// <summary>
/// Implementation of <see cref="IImageOrchestrationStatusProvider"/> that checks for file existence to determine
/// status
/// </summary>
public class FileBasedStatusProvider : IImageOrchestrationStatusProvider
{
    private readonly IOptions<OrchestratorSettings> orchestratorSettings;
    private readonly AsyncKeyedLock asyncLocker = new();
    
    public FileBasedStatusProvider(
        IOptions<OrchestratorSettings> orchestratorSettings
    )
    {
        this.orchestratorSettings = orchestratorSettings;
    }
    
    public async Task<OrchestrationStatus> GetOrchestrationStatus(AssetId assetId,
        CancellationToken cancellationToken = default)
    {
        if (DoesFileForAssetExist(assetId))
        {
            return OrchestrationStatus.Orchestrated;
        }

        return await IsOrchestrating(assetId, cancellationToken)
            ? OrchestrationStatus.Orchestrating
            : OrchestrationStatus.NotOrchestrated;
    }

    private bool DoesFileForAssetExist(AssetId assetId)
    {
        var localPath = GetLocalPath(assetId);
        if (File.Exists(localPath))
        {
            File.SetLastWriteTimeUtc(localPath, DateTime.UtcNow);
            return true;
        }

        return false;
    }

    private string GetLocalPath(AssetId assetId) => orchestratorSettings.Value.GetImageLocalPath(assetId);

    private async Task<bool> IsOrchestrating(AssetId assetId, CancellationToken cancellationToken = default)
    {
        // How to tell if Orchestrating? Use Asynclocker here?
        var lockKey = ImageOrchestrationKeys.GetOrchestrationLockKey(assetId);
        
        using var updateLock = await asyncLocker.LockAsync(lockKey, TimeSpan.Zero, false, cancellationToken);
        return !updateLock.HaveLock;
    }
}