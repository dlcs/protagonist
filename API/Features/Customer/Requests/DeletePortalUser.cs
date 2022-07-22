using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Repository;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

public class DeletePortalUser : IRequest<DeletePortalUserResult>
{
    public DeletePortalUser(int customerId, string userId)
    {
        CustomerId = customerId;
        UserId = userId;
    }

    public int CustomerId { get; }
    public string UserId { get; }
    
}


public class DeletePortalUserResult
{
    public string Error { get; set; }
}

public class DeletePortalUserHandler : IRequestHandler<DeletePortalUser, DeletePortalUserResult>
{
    private readonly DlcsContext dbContext;
    private readonly ILogger<DeletePortalUserHandler> logger;


    public DeletePortalUserHandler(
        DlcsContext dbContext,
        ILogger<DeletePortalUserHandler> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }


    public async Task<DeletePortalUserResult> Handle(DeletePortalUser request, CancellationToken cancellationToken)
    {
        var dbUser = await dbContext.Users.FindAsync(new object?[]{request.UserId}, cancellationToken);
        if (dbUser == null)
        {
            return new DeletePortalUserResult { Error = "No such user" };
        }

        if (dbUser.Customer != request.CustomerId)
        {
            logger.LogWarning("Attempt to delete another customer's user.");
            return new DeletePortalUserResult { Error = "Unable to delete user." };
        }

        dbContext.Users.Remove(dbUser);
        int i = await dbContext.SaveChangesAsync(cancellationToken);
        if (i == 1)
        {
            return new DeletePortalUserResult();
        }

        return new DeletePortalUserResult { Error = "Unable to delete user." };
    }
}