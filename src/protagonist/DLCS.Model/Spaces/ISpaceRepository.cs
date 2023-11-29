using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;

namespace DLCS.Model.Spaces;

public interface ISpaceRepository
{
    Task<int?> GetImageCountForSpace(int customerId, int spaceId);

    Task<Space?> GetSpace(int customerId, int spaceId, CancellationToken cancellationToken);
    
    Task<Space?> GetSpace(int customerId, int spaceId, bool noCache, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a space by name
    /// </summary>
    /// <param name="customerId">The customer to retrieve a space from</param>
    /// <param name="name">The name of the space</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A space, or null if it can't be found</returns>
    Task<Space?> GetSpace(int customerId, string name, CancellationToken cancellationToken);

    Task<Space> CreateSpace(int customer, string name, string? imageBucket, string[]? tags, string[]? roles,
        int? maxUnauthorised, CancellationToken cancellationToken);

    Task<PageOfSpaces> GetPageOfSpaces(int customerId, int page, int pageSize, string orderBy, bool descending,
        CancellationToken cancellationToken);

    Task<Space> PatchSpace(int customerId, int spaceId, string? name, int? maxUnauthorised, string[]? tags,
        string[]? roles, CancellationToken cancellationToken);
    
    Task<Space> PutSpace(int customerId, int spaceId, string? name, string? imageBucket, int? maxUnauthorised, string[]? tags,
        string[]? roles, CancellationToken cancellationToken);

    Task<ResultMessage<DeleteResult>> DeleteSpace(int customerId, int spaceId, CancellationToken cancellationToken);
}