using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Messaging;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.CustomHeaders.Requests;

public class DeleteCustomHeader: IRequest<ResultMessage<DeleteResult>>
{
    public DeleteCustomHeader(int customerId, string customHeaderId)
    {
        CustomerId = customerId;
        CustomHeaderId = customHeaderId;
    }
    
    public int CustomerId { get; }
    
    public string CustomHeaderId { get; }
}

public class DeleteCustomHeaderHandler : IRequestHandler<DeleteCustomHeader, ResultMessage<DeleteResult>>
{
    private readonly DlcsContext dbContext;

    public DeleteCustomHeaderHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ResultMessage<DeleteResult>> Handle(DeleteCustomHeader request, CancellationToken cancellationToken)
    {
        var customHeader = await dbContext.CustomHeaders.SingleOrDefaultAsync(
            ch => ch.Customer == request.CustomerId && 
                  ch.Id == request.CustomHeaderId,
            cancellationToken: cancellationToken);

        if (customHeader == null)
        {
            return new ResultMessage<DeleteResult>(
                $"Deletion failed - Custom Header {request.CustomHeaderId} was not found", DeleteResult.NotFound);
        }

        dbContext.CustomHeaders.Remove(customHeader);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new ResultMessage<DeleteResult>(
            $"Custom Header {request.CustomHeaderId} successfully deleted", DeleteResult.Deleted);
    }
}