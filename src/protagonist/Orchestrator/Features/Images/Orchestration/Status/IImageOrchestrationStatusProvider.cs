using DLCS.Core.Types;
using Orchestrator.Assets;

namespace Orchestrator.Features.Images.Orchestration.Status;

public interface IImageOrchestrationStatusProvider
{
    /// <summary>
    /// Get the current <see cref="OrchestrationStatus"/> of specified asset
    /// </summary>
    OrchestrationStatus GetOrchestrationStatus(AssetId assetId);
}