using System.Threading;
using System.Threading.Tasks;
using API.Client;
using MediatR;
using Portal.Behaviours;

namespace Portal.Features.Users.Requests;

/// <summary>
/// Delete Portal user by Id
/// </summary>
public class DeletePortalUser : IRequest<bool>, IAuditable
{
    public string UserId { get; }

    public DeletePortalUser(string userId)
    {
        UserId = userId;
    }
}

public class DeletePortalUserHandler : IRequestHandler<DeletePortalUser, bool>
{
    private readonly IDlcsClient dlcsClient;

    public DeletePortalUserHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public Task<bool> Handle(DeletePortalUser request, CancellationToken cancellationToken)
    {
        return dlcsClient.DeletePortalUser(request.UserId);
    }
}