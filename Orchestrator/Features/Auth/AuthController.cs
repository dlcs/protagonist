using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Features.Auth.Commands;

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
        /// <param name="customer"></param>
        /// <returns></returns>
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
        /// </summary>
        [Route("{customer}/token")]
        [HttpGet]
        public async Task<IActionResult> Token(int customer, string? messageId, string? origin)
        {
            // manually validate cookies
            return Ok();
        }
    }
}