using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Mediatr.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Portal.Behaviours
{
    /// <summary>
    /// Audits request by logging message type/contents and UserId
    /// </summary>
    public class AuditBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditable
    {
        private readonly ILogger<AuditBehaviour<TRequest, TResponse>> logger;
        private readonly ClaimsPrincipal claimsPrincipal;

        public AuditBehaviour(ILogger<AuditBehaviour<TRequest, TResponse>> logger, ClaimsPrincipal claimsPrincipal)
        {
            this.logger = logger;
            this.claimsPrincipal = claimsPrincipal;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken,
            RequestHandlerDelegate<TResponse> next)
        {
            var response = await next();
            logger.LogInformation("User '{UserId}': req:{@Request} - res:{@Response}", claimsPrincipal.GetUserId(),
                request, response);
            
            return response;
        }
    }
}