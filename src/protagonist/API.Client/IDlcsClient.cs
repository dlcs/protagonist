using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.HydraModel;
using Hydra.Collections;

namespace API.Client;

public interface IDlcsClient
{
    Task<HydraCollection<Space>?> GetSpaces(int page, int pageSize, 
        string? orderBy = null, bool descending = false, int? customerId = null);
    Task<Space> GetSpaceDetails(int spaceId);
    Task<HydraCollection<Image>> GetSpaceImages(int spaceId);
    Task<HydraCollection<Image>> GetSpaceImages(int page, int pageSize, int spaceId, 
        string? orderBy = null, bool descending = false);
    Task<CustomerStorage?> GetSpaceStorage(int spaceId);
    Task<Space?> CreateSpace(Space newSpace);
    Task<Space?> PatchSpace(int spaceId, Space space);
    Task<IEnumerable<string>?> GetApiKeys();
    Task<ApiKey> CreateNewApiKey();
    Task<Image> GetImage(int requestSpaceId, string requestImageId);
    Task<ImageStorage> GetImageStorage(int requestSpaceId, string requestImageId);
    Task<HydraCollection<PortalUser>?> GetPortalUsers();
    Task<PortalUser> CreatePortalUser(PortalUser portalUser);
    Task<bool> DeletePortalUser(string portalUserId);
    Task<Image?> DirectIngestImage(int spaceId, string imageId, Image asset);
    Task<Image?> ReingestImage(int spaceId, string imageId);
    Task<bool> DeleteImage(int spaceId, string imageId);
    Task<Image> PatchImage(Image image, int spaceId, string imageId);
    Task<HydraCollection<Image>> PatchImages(HydraCollection<Image> images, int spaceId);
    Task<HydraCollection<Batch>> GetBatches(string type, int page, int pageSize);
    Task<Batch> GetBatch(int batchId);
    Task<Batch> CreateBatch(HydraCollection<Image> images);
    Task<bool> TestBatch(int batchId);
    Task<HydraCollection<Image>> GetBatchImages(int batchId, int page, int pageSize);
    Task<CustomerQueue> GetQueue();
    Task<IEnumerable<NamedQuery>> GetNamedQueries(bool includeGlobal);
    Task DeleteNamedQuery(string namedQueryId);
    Task UpdateNamedQuery(string namedQueryId, string template);
    Task<NamedQuery> CreateNamedQuery(NamedQuery newNamedQuery);
}