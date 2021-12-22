using System;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Web.Constraints;
using IIIF.Auth.V1.AccessTokenService;
using IIIF.Serialisation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Features.Auth.Requests;

namespace Orchestrator.Features.Auth
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IMediator mediator;

        public AuthController(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        /// <summary>
        /// Handle clickthrough auth request - create a new auth cookie and return View for user to close
        /// </summary>
        [Route("{customer}/clickthrough")]
        [HttpGet]
        public async Task<IActionResult> Clickthrough(int customer)
        {
            var result = await mediator.Send(new IssueAuthToken(customer, "clickthrough"));

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
        [HttpGet]
        public async Task<IActionResult> Token(int customer, string? messageId, string? origin)
        {
            var result = await mediator.Send(new AccessTokenService(customer, messageId));

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
        [HttpGet]
        public async Task<IActionResult> InitiateAuthService(int customer, string authService)
        {
            var loginUri = await mediator.Send(new LoginWorkflow(customer, authService));

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
        /// <returns></returns>
        [Route("{customer}/{authService}")]
        [HttpGet]
        public async Task<IActionResult> RoleProviderToken(int customer, string authService,
            [RequiredFromQuery] string token)
        {
            var result = await mediator.Send(new ProcessRoleProviderToken(customer, authService, token));

            if (result.CookieCreated)
            {
                return View("CloseWindow");
            }

            return BadRequest();
        }

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
}