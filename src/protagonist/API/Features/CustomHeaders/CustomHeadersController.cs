using API.Features.CustomHeaders.Converters;
using API.Features.CustomHeaders.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.CustomHeaders;

[Route("/customers/{customerId}/customHeaders")]
[ApiController]
public class CustomHeadersController : HydraController
{
    public CustomHeadersController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get a list of custom headers owned by the user
    /// </summary>
    /// <returns>HydraCollection of CustomHeader</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomHeaders(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var namedQueries = new GetAllCustomHeaders(customerId);

        return await HandleListFetch<DLCS.Model.Assets.CustomHeaders.CustomHeader, GetAllCustomHeaders, CustomHeader>(
            namedQueries,
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get custom headers",
            cancellationToken: cancellationToken);
    }
}