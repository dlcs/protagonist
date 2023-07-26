using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Messaging;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests;

public class DeleteNamedQuery: IRequest<ResultMessage<DeleteResult>>
{
    public DeleteNamedQuery(int customerId, string namedQueryId)
    {
        CustomerId = customerId;
        NamedQueryId = namedQueryId;
    }
    
    public int CustomerId { get; }
    
    public string NamedQueryId { get; }
}

public class DeleteNamedQueryHandler : IRequestHandler<DeleteNamedQuery, ResultMessage<DeleteResult>>
{
    private readonly DlcsContext dbContext;

    public DeleteNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ResultMessage<DeleteResult>> Handle(DeleteNamedQuery request, CancellationToken cancellationToken)
    {
        var deleteResult = DeleteResult.NotFound;
        var message = string.Empty;
        
        var namedQuery = await dbContext.NamedQueries.AsNoTracking().SingleOrDefaultAsync(
            nq => nq.Customer == request.CustomerId && 
                    nq.Id == request.NamedQueryId,
                    cancellationToken: cancellationToken);

        if (namedQuery == null) return new ResultMessage<DeleteResult>(message, deleteResult);

        dbContext.NamedQueries.Remove(namedQuery);
        await dbContext.SaveChangesAsync(cancellationToken);
        deleteResult = DeleteResult.Deleted;

        return new ResultMessage<DeleteResult>(message, deleteResult);
    }
}