using System.Threading;
using System.Threading.Tasks;
using IIIF;
using Orchestrator.Assets;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Basic http client for making requests to image-servers
/// </summary>
public interface IImageServerClient
{
    Task<TImageService?> GetInfoJson<TImageService>(OrchestrationImage orchestrationImage,
        IIIF.ImageApi.Version version,
        CancellationToken cancellationToken = default)
        where TImageService : JsonLdBase;
}