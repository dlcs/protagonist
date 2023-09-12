using API.Infrastructure.Requests;
using DLCS.Model.Storage;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Storage.Requests;

public class GetSpaceStorage : IRequest<FetchEntityResult<CustomerStorage>>
{
    public int CustomerId { get; }
    
    public int SpaceId { get; }
    
    public GetSpaceStorage(int customerId, int spaceId)
    {
        CustomerId = customerId;
        SpaceId = spaceId;
    }
}

public class GetSpaceStorageHandler : IRequestHandler<GetSpaceStorage, FetchEntityResult<CustomerStorage>>
{
    private readonly DlcsContext dbContext;

    public GetSpaceStorageHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<FetchEntityResult<CustomerStorage>> Handle(GetSpaceStorage request, CancellationToken cancellationToken)
    {
        var storage = await dbContext.CustomerStorages.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Customer == request.CustomerId && s.Space == request.SpaceId, 
                cancellationToken: cancellationToken);
        
        return storage == null
            ? FetchEntityResult<CustomerStorage>.NotFound()
            : FetchEntityResult<CustomerStorage>.Success(storage);
    }
}