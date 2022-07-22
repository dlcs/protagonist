using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Repository;
using MediatR;

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


    public DeletePortalUserHandler(
        DlcsContext dbContext)
    {
        this.dbContext = dbContext;
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
            return new DeletePortalUserResult { Error = "User doesn't belong to customer" };
        }

        dbContext.Users.Remove(dbUser);
        int i = await dbContext.SaveChangesAsync(cancellationToken);
        if (i == 1)
        {
            return new DeletePortalUserResult();
        }

        return new DeletePortalUserResult { Error = "Unable to delete user" };
    }
}