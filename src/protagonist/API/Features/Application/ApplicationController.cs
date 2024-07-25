using API.Features.Application.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Application;

/// <summary>
/// API operations for application wide configuration
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
[AllowAnonymous]
public class ApplicationController : HydraController
{
    public ApplicationController(IOptions<ApiSettings> settings, IMediator mediator) : base(settings.Value, mediator)
    {
    }

    [Route("/setup")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiKey))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(Error))]
    public async Task<IActionResult> SetupApplication()
    {
        var request = new SetupApplication();
        var result = await Mediator.Send(request);

        return result.CreateSuccess
            ? Ok(new ApiKey(GetUrlRoots().BaseUrl, 1, result.Key, result.Secret))
            : this.HydraProblem("Unable to setup application", null, 500, "Setup");
    }
}