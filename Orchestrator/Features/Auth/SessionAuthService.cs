using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using DLCS.Repository;
using DLCS.Repository.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Features.Auth
{
    /// <summary>
    /// Contains methods for dealing with AuthToken and SessionUsers.
    /// </summary>
    public interface ISessionAuthService
    {
        /// <summary>
        /// Create a new Session and AuthToken for specified customer.
        /// </summary>
        /// <param name="customer">Current customer.</param>
        /// <param name="authServiceName">Name of auth service to add to Session (e.g. "clickthrough")</param>
        /// <returns>New AuthToken</returns>
        Task<AuthToken?> CreateAuthTokenForRole(int customer, string authServiceName);

        /// <summary>
        /// Get <see cref="AuthToken"/> for provided cookieId. Expiry will be refreshed.
        /// </summary>
        /// <param name="customer">Current customer.</param>
        /// <param name="cookieId">CookieId to get AuthToken for</param>
        /// <returns>AuthToken if found and not expired, else null</returns>
        Task<AuthToken?> GetAuthTokenForCookieId(int customer, string cookieId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get <see cref="AuthToken"/> for provided bearer token. Expiry will be refreshed.
        /// </summary>
        /// <param name="customer">Current customer.</param>
        /// <param name="bearerToken">Bearer token to get SessionUser for</param>
        /// <returns>AuthToken if found and not expired, else null</returns>
        Task<AuthToken?> GetAuthTokenForBearerId(int customer, string bearerToken,
            CancellationToken cancellationToken = default);
    }

    public class SessionAuthService : ISessionAuthService
    {
        // AuthTokens are not refreshed if they were LastChecked within this threshold
        private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(2);
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
            var authService = await dbContext.AuthServices
                .AsNoTracking()
                .SingleOrDefaultAsync(authSvc => authSvc.Customer == customer && authSvc.Name == authServiceName);
            
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

        /// <summary>
        /// Get <see cref="AuthToken"/> for provided cookieId. Expiry will be refreshed.
        /// </summary>
        /// <param name="customer">Current customer.</param>
        /// <param name="cookieId">CookieId to get AuthToken for</param>
        /// <returns>AuthToken if found and not expired, else null</returns>
        public async Task<AuthToken?> GetAuthTokenForCookieId(int customer, string cookieId,
            CancellationToken cancellationToken = default)
        {
            var authToken =
                await GetRefreshedAuthToken(customer, token => token.CookieId == cookieId, cancellationToken);

            if (authToken == null)
            {
                logger.LogInformation(
                    "Requested authToken for customer:'{Customer}', cookie:'{CookieId}' not found or expired",
                    customer, cookieId);
                return null;
            }

            return authToken;
        }

        public async Task<AuthToken?> GetAuthTokenForBearerId(int customer, string bearerToken,
            CancellationToken cancellationToken = default)
        {
            var authToken =
                await GetRefreshedAuthToken(customer, token => token.BearerToken == bearerToken, cancellationToken);

            if (authToken == null)
            {
                logger.LogInformation(
                    "Requested authToken for customer:'{Customer}', bearerToken:'{BearerToken}' not found or expired",
                    customer, bearerToken);
                return null;
            }

            return authToken;
        }

        private async Task<AuthToken?> GetRefreshedAuthToken(int customer, Expression<Func<AuthToken, bool>> additionalPredicate,
            CancellationToken cancellationToken)
        {
            var authToken = await dbContext.AuthTokens
                .Where(token => token.Customer == customer)
                .SingleOrDefaultAsync(additionalPredicate, cancellationToken);

            if (authToken == null)
            {
                logger.LogDebug(
                    "Could not find requested authToken for customer:'{Customer}'", customer);
                return null;
            }

            if (authToken.Expires <= DateTime.Now)
            {
                logger.LogDebug("AuthToken expired, customer:'{Customer}'", customer);
                return null;
            }

            // Token was last checked in the past and threshold has passed
            if (authToken.LastChecked.HasValue && authToken.LastChecked.Value.Add(RefreshThreshold) < DateTime.Now)
            {
                authToken.LastChecked = DateTime.Now;
                authToken.Expires = DateTime.Now.AddSeconds(authToken.Ttl);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            authToken.SessionUser = await dbContext.SessionUsers.FindAsync(authToken.SessionUserId);

            return authToken;
        }

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