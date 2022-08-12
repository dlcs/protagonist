using System;
using System.IO;
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

    public FileBasedStatusProvider(IOptions<OrchestratorSettings> orchestratorSettings)
    {
        this.orchestratorSettings = orchestratorSettings;
    }

    public OrchestrationStatus GetOrchestrationStatus(AssetId assetId)
        => DoesFileForAssetExist(assetId) ? OrchestrationStatus.Orchestrated : OrchestrationStatus.NotOrchestrated;

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
}