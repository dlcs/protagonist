using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.Spaces;

public interface ISpaceRepository
{
    Task<int?> GetImageCountForSpace(int customerId, int spaceId);

    Task<Space?> GetSpace(int customerId, int spaceId, CancellationToken cancellationToken);
    Task<Space?> GetSpace(int customerId, int spaceId, CancellationToken cancellationToken, bool noCache);
    Task<Space?> GetSpace(int customerId, string name, CancellationToken cancellationToken);
    Task<Space> CreateSpace(int customer, string name, string? imageBucket, 
        string[]? tags, string[]? roles, int? maxUnauthorised, CancellationToken cancellationToken);

    Task<PageOfSpaces> GetPageOfSpaces(int customerId, int page, int pageSize,
        string orderBy, bool descending, 
        CancellationToken cancellationToken);
    Task<Space> PatchSpace(int customerId, int spaceId, string? name,
        int? maxUnauthorised, string[]? tags, string[]? roles, CancellationToken cancellationToken);
}