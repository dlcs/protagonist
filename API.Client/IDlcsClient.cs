using System.Collections.Generic;
using System.Threading.Tasks;
using API.Client.JsonLd;

namespace API.Client
{
    public interface IDlcsClient
    {
        Task<Space?> GetSpaceDetails(int spaceId);
        Task<HydraImageCollection> GetSpaceImages(int spaceId);
        Task<Space?> CreateSpace(Space newSpace);
        Task<IEnumerable<string>?> GetApiKeys();
        Task<ApiKey> CreateNewApiKey();
        Task<Image> GetImage(int requestSpaceId, string requestImageId);
        Task<SimpleCollection<PortalUser>?> GetPortalUsers();
        Task<PortalUser> CreatePortalUser(PortalUser portalUser);
        Task<bool> DeletePortalUser(string portalUserId);
        Task<AssetJsonLD?> DirectIngestImage(int spaceId, string imageId, AssetJsonLD asset);
        Task<HydraImageCollection> PatchImages(HydraImageCollection images, int spaceId);
    }
}