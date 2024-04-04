using Engine.Ingest.Image.ImageServer.Models;

namespace Engine.Ingest.Image.ImageServer.Clients;

public interface IAppetiserClient
{
    /// <summary>
    /// Calls appetiser to generate an image
    /// </summary>
    /// <param name="requestModel">The request model used to generate an image</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A response containing details of the generated image</returns>
    public Task<IAppetiserResponse> CallAppetiser(AppetiserRequestModel requestModel, CancellationToken cancellationToken = default);
}