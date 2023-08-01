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
        var deleteResult = DeleteResult.NotFound;
        var message = string.Empty;
        
        var customHeader = await dbContext.CustomHeaders.SingleOrDefaultAsync(
            ch => ch.Customer == request.CustomerId && 
                  ch.Id == request.CustomHeaderId,
            cancellationToken: cancellationToken);

        if (customHeader == null)
        {
            message = $"Deletion failed - Custom Header {request.CustomHeaderId} was not found";
            return new ResultMessage<DeleteResult>(message, deleteResult);
        }

        dbContext.CustomHeaders.Remove(customHeader);
        await dbContext.SaveChangesAsync(cancellationToken);
        deleteResult = DeleteResult.Deleted;

        return new ResultMessage<DeleteResult>(message, deleteResult);
    }
}