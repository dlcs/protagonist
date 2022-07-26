using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Auth;
using MediatR;

namespace Orchestrator.Features.Auth.Requests;

/// <summary>
/// Initiate login for specified auth service
/// </summary>
public class LoginWorkflow : IRequest<Uri?>
{
    public int CustomerId { get; }
    
    public string AuthServiceName { get; }

    public LoginWorkflow(int customerId, string authServiceName)
    {
        CustomerId = customerId;
        AuthServiceName = authServiceName;
    }
}

public class LoginWorkflowHandler : IRequestHandler<LoginWorkflow, Uri?>
{
    private readonly IRoleProviderService roleProviderService;

    public LoginWorkflowHandler(IRoleProviderService roleProviderService)
    {
        this.roleProviderService = roleProviderService;
    }
    
    public async Task<Uri?> Handle(LoginWorkflow request, CancellationToken cancellationToken)
    {
        var configuration =
            await roleProviderService.GetRoleProviderConfiguration(request.CustomerId, request.AuthServiceName);
        return configuration?.Target;
    }
}