using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Security;
using DLCS.Repository;
using DLCS.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Features.Auth
{
    public class SessionAuthService
    {
        private readonly DlcsContext dbContext;
        private readonly ILogger<SessionAuthService> logger;

        public SessionAuthService(
            DlcsContext dbContext,
            ILogger<SessionAuthService> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }
        
        /// <summary>
        /// Create a new Session and AuthToken for specified customer.
        /// </summary>
        /// <param name="customer">Current customer.</param>
        /// <param name="authServiceName">Name of auth service to add to Session (e.g. "clickthrough")</param>
        /// <returns>New AuthToken</returns>
        public async Task<AuthToken?> CreateAuthTokenForRole(int customer, string authServiceName)
        {
            var authService = await GetAuthService(customer, authServiceName);
            
            if (authService == null)
            {
                logger.LogInformation("Could not find AuthService '{Name}' for customer '{Customer}'", authServiceName,
                    customer);
                return null;
            }
            
            // Now create a new AuthToken and SessionUser record
            var sessionUser = await CreateSessionUser(customer, authService.Id);
            var authToken = await CreateAuthToken(authService, sessionUser);

            await dbContext.SaveChangesAsync();

            return authToken;
        }

        private async Task<AuthService?> GetAuthService(int customer, string authServiceName)
            => await dbContext.AuthServices
                .AsNoTracking()
                .SingleOrDefaultAsync(authSvc => authSvc.Customer == customer && authSvc.Name == authServiceName);

        private async Task<SessionUser> CreateSessionUser(int customer, params string[] authServiceName)
        {
            var sessionUser = new SessionUser
            {
                Created = DateTime.Now,
                Id = Guid.NewGuid().ToString(),
                Roles = new Dictionary<int, List<string>>
                {
                    [customer] = authServiceName.ToList()
                }
            };

            await dbContext.SessionUsers.AddAsync(sessionUser);
            return sessionUser;
        }

        private async Task<AuthToken> CreateAuthToken(AuthService authService, SessionUser sessionUser)
        {
            var authToken = new AuthToken
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.Now,
                LastChecked = DateTime.Now,
                CookieId = Guid.NewGuid().ToString(),
                SessionUserId = sessionUser.Id,
                BearerToken = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Customer = authService.Customer,
                Expires = DateTime.Now.AddSeconds(authService.Ttl),
                Ttl = authService.Ttl
            };
            await dbContext.AuthTokens.AddAsync(authToken);
            return authToken;
        }
    }
}