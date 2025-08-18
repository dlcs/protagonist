using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DLCS.Core.Strings;
using DLCS.Model.Customers;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace API.Auth;

/// <summary>
/// Basic authentication handler for DLCS
/// </summary>
/// <remarks>
/// Auth in DLCS API
///  - Some paths, such as https://api.dlcs.io/ and various shared policies, can serve GET requests
///    to an anonymous user. Another example: https://api.dlcs.io/originStrategies
///  - An admin API key is allowed to make any valid API call.
///  - A customer path is one that starts /customers/n/, where n is the customer id.
///    Customer-specific resources such as images, auth services, etc. live under these paths.
///  - Only an admin key or a key belonging to that customer can call these paths.
///  - A customer path is never anonymous.
/// 
/// Care should be taken not to confuse the customer whose resource is being called with the
/// customer making the call.
/// When the caller is admin, these can be different.
/// The resource might not be associated with any customer.
/// </remarks>
public class DlcsBasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
    private readonly ICustomerRepository customerRepository;
    private readonly DlcsApiAuth dlcsApiAuth;
    private readonly JwtAuthHelper authHelper;

    private static readonly JsonWebTokenHandler JwtHandler = new();

    /// <summary>
    /// Deduces the caller's claims
    ///  - what customer are they?
    ///  - are they admin?
    ///  - If they are calling a resource under customers/x/, are they x?
    ///
    /// Downstream controllers may still reject calls for other reasons.
    /// </summary>
    public DlcsBasicAuthenticationHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICustomerRepository customerRepository,
        DlcsApiAuth dlcsApiAuth,
        JwtAuthHelper authHelper)
        : base(options, logger, encoder)
    {
        this.customerRepository = customerRepository;
        this.dlcsApiAuth = dlcsApiAuth;
        this.authHelper = authHelper;
    }

    /// <summary>
    /// Called by the ASP.NET pipeline
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // skip authentication if endpoint has [AllowAnonymous] attribute
        // Not all API paths require auth...
        var endpoint = Context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            return AuthenticateResult.NoResult();
        }

        // ...but any not marked must have the auth header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Missing Authorization Header in request");
        }

        var headerValue = Request.GetAuthHeaderValue(AuthenticationHeaderUtils.BasicScheme)
                          ?? Request.GetAuthHeaderValue(AuthenticationHeaderUtils.BearerTokenScheme);

        if (headerValue == null)
        {
            return AuthenticateResult.Fail("Missing Authorization Header in request");
        }

        // for a path like /customers/23/queue, the resourceCustomerId is 23.
        // This isn't necessarily the customer that owns the api key being used on the call!
        int? resourceCustomerId = null;
        if (Request.RouteValues.TryGetValue("customerId", out var customerIdRouteVal)
            && customerIdRouteVal != null
            && int.TryParse(customerIdRouteVal.ToString(), out var result))
        {
            resourceCustomerId = result;
        }

        return await GetApiCaller(headerValue, resourceCustomerId) switch
        {
            null => AuthenticateResult.Fail("Invalid credentials"),
            FailedCaller fail => AuthenticateResult.Fail(fail.Message),
            // Success:
            ApiCaller apiCaller => AuthenticateApiCaller(apiCaller, resourceCustomerId),
            // Unlikely:
            _ => throw new InvalidOperationException()
        };
    }

    private AuthenticateResult AuthenticateApiCaller(ApiCaller apiCaller, int? resourceCustomerId)
    {
        if (apiCaller.Customer is not { } customer)
        {
            Logger.LogError("A non-fail ApiCaller returned with null Customer field");
            return AuthenticateResult.Fail("Unexpected error");
        }

        // We still have some checks to do before authenticating this user.
        // Some of these could be classed as authorisation rather than authentication, though they are not user-specific.
        if (resourceCustomerId.HasValue)
        {
            // the request is for a particular customer's resource (e.g., an asset)
            if (customer.Id != resourceCustomerId.Value)
            {
                // ... but the requester is not this customer. Only proceed if they are admin.
                if (!customer.Administrator)
                {
                    return AuthenticateResult.Fail("Only admin user may access this customer's resource.");
                }

                Logger.LogDebug("Admin key accessing a customer's resource");
            }
        }
        else
        {
            // The request isn't for a customer resource (i.e., under the path /customers/n/).
            // Only proceed if they are admin.
            if (!customer.Administrator)
            {
                return AuthenticateResult.Fail("Only admin user may access this shared resource");
            }

            Logger.LogDebug("Admin key accessing a shared resource");
        }

        // At this point our *authentication* has passed.
        // Downstream handlers may still refuse to *authorise* the request for other reasons
        // (it can still end up a 401)
        Logger.LogTrace("Authentication passed");

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, customer.Name), // TODO - should these be the other way round?
            new(ClaimTypes.NameIdentifier, apiCaller.Key), // ^^^
            new(ClaimsPrincipalUtils.Claims.Customer, customer.Id.ToString()),
            new(ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Customer),
        };
        if (customer.Administrator)
        {
            claims.Add(new(ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Admin));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private async Task<IApiCaller?> GetApiCaller(AuthenticationHeaderValue headerValue, int? customerIdHint)
    {
        switch (headerValue.Scheme)
        {
            case AuthenticationHeaderUtils.BasicScheme:
                return await GetApiCallerFromBasic(headerValue.Parameter, customerIdHint);
            case AuthenticationHeaderUtils.BearerTokenScheme:
                return await GetApiCallerFromJwt(headerValue.Parameter);
        }

        Logger.LogError("Could not parse auth header");
        return null;
    }

    private async Task<IApiCaller?> GetApiCallerFromBasic(string? headerValueParameter, int? customerIdHint)
    {
        var parts = headerValueParameter?.DecodeBase64().Split(':');
        if (parts?.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
        {
            return null;
        }
        var key = parts[0];
        var secret = parts[1];
        
        var customerForKey = await customerRepository.GetCustomerForKey(key, customerIdHint);
        if (customerForKey == null)
        {
            return new FailedCaller("No customer found for this key that is permitted to access this resource");
        }

        if (secret != dlcsApiAuth.GetApiSecret(customerForKey, Options.Salt, key))
        {
            return new FailedCaller("Invalid credentials");
        }

        return new ApiCaller(key, customerForKey);

    }

    private async Task<IApiCaller?> GetApiCallerFromJwt(string? token)
    {
        if (authHelper.SigningCredentials?.Key is not { } signingKey)
        {
            return new FailedCaller("JWT not enabled");
        }

        if (token is null)
        {
            return new FailedCaller("JWT missing");
        }

        var result = await JwtHandler.ValidateTokenAsync(token, new()
        {
            IssuerSigningKey = signingKey,
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidIssuers = authHelper.ValidIssuers
        });

        if (!result.IsValid)
        {
            return new FailedCaller("Invalid JWT used");
        }

        if (!result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subUrnObj) ||
            subUrnObj is not string subUrn)
        {
            return new FailedCaller("JWT missing sub field");
        }

        // RFC 8141:
        // NID - Namespace Identifier - here "dlcs" to have own namespace
        // NSS - Namespace Specific String - currently we use "user" to inform that the next part is user (customer) id
        const string dlcsUrnNamespace = "dlcs";
        const string dlcsUrnUser = "user";

        // In modern C#:
        // if (subUrn.Split(':') is not [_, var nid, var nss, var id, ..])
        // {
        //     return new FailedCaller("JWT sub in incorrect urn format");
        // }

        var parts = subUrn.Split(':');
        if (parts.Length < 4)
        {
            return new FailedCaller("JWT sub in incorrect urn format");
        }

        var nid = parts[1];
        var nss = parts[2];
        var id = parts[3];
        
        // Future integration: nid/nss/id can be used for granular permissions
        // For now, we have 1 customer, the Customer Portal, so "hardcode" the
        // verification.

        // We only support the following subjects: urn:dlcs:user:<customerId>
        if (!dlcsUrnNamespace.Equals(nid) || !dlcsUrnUser.Equals(nss))
        {
            return new FailedCaller("Unsupported/unauthorised token data");
        }

        if (!int.TryParse(id, out var customerId))
        {
            return new FailedCaller("Invalid customer id format");
        }
        
        Logger.LogDebug("Using JWT with customer id {CustomerId}", customerId);

        var customer = await customerRepository.GetCustomer(customerId);
        if (customer is null)
        {
            return new FailedCaller("Customer not found");
        }

        return new ApiCaller(customer.Keys.First(), customer);
    }

    /// <summary>
    /// Called by the ASP.NET pipeline
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Options.Realm}\"";
        return base.HandleChallengeAsync(properties);
    }
}

/// <summary>
/// Can be <see cref="ApiCaller"/> or <see cref="FailedCaller"/>
/// </summary>
public interface IApiCaller
{
}

/// <summary>
/// Represents the credentials in the API request, the basic auth key:secret pair.
/// </summary>
public class ApiCaller : IApiCaller
{
    public ApiCaller(string key, Customer? customer)
    {
        Key = key;
        Customer = customer;
    }

    public string Key { get; }

    public Customer? Customer { get; }
}

public class FailedCaller : IApiCaller
{
    public FailedCaller(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
