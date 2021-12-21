using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Orchestrator.Features.Auth.Requests
{
    /// <summary>
    /// Initiate login for specified auth service
    /// </summary>
    public class LoginWorkflow : IRequest<Uri?>
    {
        public int Customer { get; }
        
        public string AuthService { get; }

        public LoginWorkflow(int customer, string authService)
        {
            Customer = customer;
            AuthService = authService;
        }
    }
    
    public class LoginWorkflowHandler : IRequestHandler<LoginWorkflow, Uri?>
    {
        private readonly IAuthServicesRepository authServicesRepository;
        private readonly IHttpContextAccessor httpContextAccessor;

        public LoginWorkflowHandler(
            IAuthServicesRepository authServicesRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            this.authServicesRepository = authServicesRepository;
            this.httpContextAccessor = httpContextAccessor;
        }
        
        public async Task<Uri?> Handle(LoginWorkflow request, CancellationToken cancellationToken)
        {
            var roleProvider = await GetRoleProviderForAuthService(request);
            if (roleProvider == null) return null;

            // Parse the config
            var configuration = GetRoleProviderConfiguration(roleProvider);
            return configuration.Target;
        }

        private async Task<RoleProvider?> GetRoleProviderForAuthService(LoginWorkflow request)
        {
            // Load auth services for this role
            var authService = await authServicesRepository.GetAuthServiceByName(request.Customer, request.AuthService);
            if (authService == null) return null;

            // Load RoleProvider for auth-service
            var roleProvider = await authServicesRepository.GetRoleProvider(authService.RoleProvider);
            return roleProvider;
        }
        
        private RoleProviderConfiguration GetRoleProviderConfiguration(RoleProvider? roleProvider)
        {
            var configBlock = RoleProviderConfigBlock.FromBase64Json(roleProvider.Configuration);
            var configuration = configBlock.GetForHost(httpContextAccessor.HttpContext.Request.Host.ToString());
            return configuration;
        }
    }
}