using DLCS.Core;
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
        var namedQuery = await dbContext.NamedQueries.SingleOrDefaultAsync(
            nq => nq.Customer == request.CustomerId && 
                    nq.Id == request.NamedQueryId,
                    cancellationToken: cancellationToken);

        if (namedQuery == null)
        {
            return new ResultMessage<DeleteResult>("Couldn't find a named query with the id {request.NamedQuery.Id}",
                DeleteResult.NotFound);
        }

        dbContext.NamedQueries.Remove(namedQuery);
        await dbContext.SaveChangesAsync(cancellationToken); ;

        return new ResultMessage<DeleteResult>(string.Empty, DeleteResult.Deleted);
    }
}