using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.HydraModel;
using Hydra.Collections;

namespace API.Client;

public interface IDlcsClient
{
    Task<HydraCollection<Space>?> GetSpaces(int page, int pageSize, 
        string? orderBy = null, bool descending = false, int? customerId = null);
    Task<Space?> GetSpaceDetails(int spaceId);
    Task<HydraCollection<Image>> GetSpaceImages(int spaceId);
    Task<HydraCollection<Image>> GetSpaceImages(int page, int pageSize, int spaceId, 
        string? orderBy = null, bool descending = false);
    Task<Space?> CreateSpace(Space newSpace);
    Task<Space?> PatchSpace(int spaceId, Space space);
    Task<IEnumerable<string>?> GetApiKeys();
    Task<ApiKey> CreateNewApiKey();
    Task<Image> GetImage(int requestSpaceId, string requestImageId);
    Task<HydraCollection<PortalUser>?> GetPortalUsers();
    Task<PortalUser> CreatePortalUser(PortalUser portalUser);
    Task<bool> DeletePortalUser(string portalUserId);
    Task<Image?> DirectIngestImage(int spaceId, string imageId, Image asset);
    Task<HydraCollection<Image>> PatchImages(HydraCollection<Image> images, int spaceId);
    
    
    Task<bool> ReingestAsset(int requestSpaceId, string requestImageId);
}