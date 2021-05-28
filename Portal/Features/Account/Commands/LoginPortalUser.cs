using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Encryption;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Settings;

namespace Portal.Features.Account.Commands
{
    public class LoginPortalUser : IRequest<bool>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        
        public string ApiKey { get; set; }
        
        public string ApiSecret { get; set; }
    }
    
    public class LoginPortalUserHandler : IRequestHandler<LoginPortalUser, bool>
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly DlcsContext dbContext;
        private readonly IEncryption encryption;
        private readonly ILogger<LoginPortalUserHandler> logger;
        private readonly PortalSettings options;

        public LoginPortalUserHandler(
            IHttpContextAccessor httpContextAccessor, 
            DlcsContext dbContext, 
            IOptions<PortalSettings> options,
            IEncryption encryption,
            ILogger<LoginPortalUserHandler> logger)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.dbContext = dbContext;
            this.encryption = encryption;
            this.logger = logger;
            this.options = options.Value;
        }

        public async Task<bool> Handle(LoginPortalUser request, CancellationToken cancellationToken)
        {
            // Get User
            var user = await GetUser(request, cancellationToken);
            if (user == null) return false;
            
            // Validate credentials
            if (!ValidateCredentials(request, user)) return false;
            
            // Log user in
            await DoLogin(user, request);
            return true;
        }

        private async Task<User?> GetUser(LoginPortalUser request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == request.Username.ToLowerInvariant() && u.Enabled,
                    cancellationToken: cancellationToken);
            return user;
        }

        private bool ValidateCredentials(LoginPortalUser request, User user)
        {
            string passwordToCheck = encryption.Encrypt(string.Concat(options.LoginSalt, request.Password));
            return passwordToCheck == user.EncryptedPassword;
        }

        private async Task DoLogin(User user, LoginPortalUser loginPortalUser)
        {
            var claimsIdentity = await GenerateClaimsIdentity(user, loginPortalUser);
            
            // TODO - what do we want here?
            var authProperties = new AuthenticationProperties
            {
                //AllowRefresh = <bool>,
                // Refreshing the authentication session should be allowed.

                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                // The time at which the authentication ticket expires. A 
                // value set here overrides the ExpireTimeSpan option of 
                // CookieAuthenticationOptions set with AddCookie.

                //IsPersistent = true,
                // Whether the authentication session is persisted across 
                // multiple requests. When used with cookies, controls
                // whether the cookie's lifetime is absolute (matching the
                // lifetime of the authentication ticket) or session-based.

                //IssuedUtc = <DateTimeOffset>,
                // The time at which the authentication ticket was issued.

                //RedirectUri = <string>
                // The full path or absolute URI to be used as an http 
                // redirect response value.
            };
            
            await httpContextAccessor.HttpContext!.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                new ClaimsPrincipal(claimsIdentity), 
                authProperties);
        }

        private async Task<ClaimsIdentity> GenerateClaimsIdentity(User user, LoginPortalUser loginPortalUser)
        {
            var claims = new List<Claim>
            {
                new (ClaimTypes.Name, user.Email),
                new (ClaimsPrincipalUtils.Claims.Customer, user.Customer.ToString()),
                new (ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Customer),
            };
            
            foreach (var role in user.Roles.Split(",", StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (await IsUserAdmin(user))
            {
                logger.LogInformation("User {UserId} logged in with admin credentials", user.Id);
                claims.Add(new Claim(ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Admin));
            }

            // TODO - this is temporary only until API supports shared auth with Portal user
            if (!string.IsNullOrEmpty(loginPortalUser.ApiKey) && !string.IsNullOrEmpty(loginPortalUser.ApiSecret))
            {
                logger.LogInformation("Api credentials provided, adding claim");

                var creds = $"{loginPortalUser.ApiKey}:{loginPortalUser.ApiSecret}";
                var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes(creds));
                claims.Add(new Claim(ClaimsPrincipalUtils.Claims.ApiCredentials, basicAuth));
            }

            return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        }
        
        private async Task<bool> IsUserAdmin(User user)
        {
            // NOTE - is this a candidate for moving to a reusable method?
            var customer = await dbContext.Customers.FindAsync(user.Customer);
            return customer.Administrator;
        }
    }
}