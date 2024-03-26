using Engine.Ingest.Image.ImageServer.Models;

namespace Engine.Ingest.Image.ImageServer.Clients;

public interface IAppetiserClient
{
    public Task<IAppetiserResponse> CallAppetiser(AppetiserRequestModel requestModel, CancellationToken cancellationToken = default);
}