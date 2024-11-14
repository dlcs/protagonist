using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Strings;
using DLCS.Web.Constraints;
using IIIF.Auth.V1.AccessTokenService;
using IIIF.Serialisation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Auth.Requests;
using Orchestrator.Infrastructure;

namespace Orchestrator.Features.Auth;

[Route("[controller]")]
[ApiController]
public class AuthController : IIIFAssetControllerBase
{
    public AuthController(IMediator mediator, IOptions<CacheSettings> cacheSettings, ILogger<AuthController> logger)
        : base(mediator, cacheSettings, logger)
    {
    }

    /// <summary>
    /// Handle clickthrough auth request - create a new auth cookie and return View for user to close
    /// </summary>
    [Route("{customer}/clickthrough")]
    [ResponseCache(NoStore = true)]
    [HttpGet]
    public async Task<IActionResult> Clickthrough(int customer)
    {
        var result = await Mediator.Send(new IssueAuthToken(customer, "clickthrough"));

        if (result.CookieCreated)
        {
            return View("CloseWindow");
        }

        return BadRequest();
    }

    /// <summary>
    /// Access Token Service handling
    /// See https://iiif.io/api/auth/1.0/#access-token-service
    /// </summary>
    [Route("{customer}/token")]
    [ResponseCache(NoStore = true)]
    [HttpGet]
    public async Task<IActionResult> Token(int customer, string? messageId, string? origin)
    {
        var result = await Mediator.Send(new AccessTokenService(customer, messageId));

        // If messageId provided, return HTML, else return JSON
        var returnHtmlRepresentation = messageId.HasText();

        string jsonResponseObject;
        HttpStatusCode httpStatusCode;

        if (result.IsSuccess)
        {
            httpStatusCode = HttpStatusCode.OK;
            jsonResponseObject = result.Response.AsJson();
        }
        else
        {
            httpStatusCode = GetStatusCodeForAccessTokenError(result.Error.Error);
            jsonResponseObject = result.Error.AsJson();
        }

        if (returnHtmlRepresentation)
        {
            return View("TokenService", jsonResponseObject);
        }
        else
        {
            Response.StatusCode = (int)httpStatusCode;
            return Content(jsonResponseObject, "application/json");
        }
    }

    /// <summary>
    /// Initiate login for auth service
    /// </summary>
    /// <param name="customer">Customer Id</param>
    /// <param name="authService">Name of authService to initiate.</param>
    /// <returns>Redirect to downstream role-provider login service</returns>
    [Route("{customer}/{authService}")]
    [ResponseCache(NoStore = true)]
    [HttpGet]
    public async Task<IActionResult> InitiateAuthService(int customer, string authService)
    {
        var loginUri = await Mediator.Send(new LoginWorkflow(customer, authService));

        return loginUri == null
            ? new NotFoundResult()
            : new RedirectResult(loginUri.ToString(), false);
    }

    /// <summary>
    /// Handle GET request from role-provider, creating new session for specified token parameter
    /// </summary>
    /// <param name="customer">Customer Id</param>
    /// <param name="authService">Name of authService.</param>
    /// <param name="token">Role-provider token</param>
    [Route("{customer}/{authService}")]
    [ResponseCache(NoStore = true)]
    [HttpGet]
    public async Task<IActionResult> RoleProviderToken(int customer, string authService,
        [RequiredFromQuery] string token)
    {
        var result = await Mediator.Send(new ProcessRoleProviderToken(customer, authService, token));

        if (result.CookieCreated)
        {
            return View("CloseWindow");
        }

        return BadRequest();
    }

    /// <summary>
    /// Log current user out of specified auth-service.
    /// </summary>
    /// <param name="customer">Customer Id</param>
    /// <param name="authService">Name of authService.</param>
    /// <returns></returns>
    [Route("{customer}/{authService}/logout")]
    [ResponseCache(NoStore = true)]
    [HttpGet]
    public async Task<IActionResult> Logout(int customer, string authService)
    {
        var logoutUri = await Mediator.Send(new LogoutAuthService(customer, authService));

        return logoutUri == null
            ? View("CloseWindow")
            : new RedirectResult(logoutUri.ToString(), false);
    }

    /// <summary>
    /// IIIF Authorization Flow 2.0 ProbeService. The probe service is used by the client to understand whether the user
    /// has access to the access-controlled resource for which the probe service is declared.
    /// </summary>
    /// <param name="customer">Customer Id</param>
    /// <param name="space">Space Id</param>
    /// <param name="image">Image Id</param>
    /// <remarks>https://iiif.io/api/auth/2.0/#probe-service</remarks>
    [Route("v2/probe/{customer}/{space}/{image}")]
    [HttpGet]
    public Task<IActionResult> ProbeService(
        [FromRoute] int customer,
        [FromRoute] int space,
        [FromRoute] string image,
        CancellationToken cancellationToken = default)
        => GenerateIIIFDescriptionResource(
            () => new ProbeService(customer, space, image), cacheTtl: 0, cancellationToken: cancellationToken);

    private HttpStatusCode GetStatusCodeForAccessTokenError(AccessTokenErrorConditions conditions)
        => conditions switch
        {
            AccessTokenErrorConditions.Unavailable => HttpStatusCode.InternalServerError,
            AccessTokenErrorConditions.InvalidRequest => HttpStatusCode.BadRequest,
            AccessTokenErrorConditions.MissingCredentials => HttpStatusCode.Unauthorized,
            AccessTokenErrorConditions.InvalidCredentials => HttpStatusCode.Forbidden,
            AccessTokenErrorConditions.InvalidOrigin => HttpStatusCode.BadRequest,
            _ => throw new ArgumentOutOfRangeException(nameof(conditions), conditions, null)
        };
}