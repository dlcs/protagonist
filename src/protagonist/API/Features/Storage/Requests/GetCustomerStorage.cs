using API.Infrastructure.Requests;
using DLCS.Model.Storage;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Storage.Requests;

public class GetCustomerStorage : IRequest<FetchEntityResult<CustomerStorage>>
{
    public int CustomerId { get; }
    
    public GetCustomerStorage(int customerId)
    {
        CustomerId = customerId;
    }
}

public class GetCustomerStorageHandler : IRequestHandler<GetCustomerStorage, FetchEntityResult<CustomerStorage>>
{
    private readonly DlcsContext dbContext;
    private const int DefaultStorageId = 0;
    
    public GetCustomerStorageHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<FetchEntityResult<CustomerStorage>> Handle(GetCustomerStorage request, CancellationToken cancellationToken)
    {
        var storage = await dbContext.CustomerStorages.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Customer == request.CustomerId && s.Space == DefaultStorageId, 
                cancellationToken: cancellationToken);
        
        return storage == null
            ? FetchEntityResult<CustomerStorage>.NotFound()
            : FetchEntityResult<CustomerStorage>.Success(storage);
    }
}