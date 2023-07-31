using API.Features.CustomHeaders.Converters;
using API.Features.CustomHeaders.Requests;
using API.Features.CustomHeaders.Validation;
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
        var customHeaders = new GetAllCustomHeaders(customerId);

        return await HandleListFetch<DLCS.Model.Assets.CustomHeaders.CustomHeader, GetAllCustomHeaders, CustomHeader>(
            customHeaders,
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get custom headers",
            cancellationToken: cancellationToken);
    }
    
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostCustomHeader(
        [FromRoute] int customerId,
        [FromBody] CustomHeader newCustomHeader,
        [FromServices] HydraCustomHeaderValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(newCustomHeader, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        newCustomHeader.CustomerId = customerId;
        var request = new CreateCustomHeader(customerId, newCustomHeader.ToDlcsModel());
        
        return await HandleUpsert(request, 
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create custom header",
            cancellationToken: cancellationToken);
    }
    
    [HttpGet]
    [Route("{customHeaderId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomHeader(
        [FromRoute] int customerId,
        [FromRoute] string customHeaderId,
        CancellationToken cancellationToken)
    {
        var customHeader = new GetCustomHeader(customerId, customHeaderId);
        
        return await HandleFetch(
            customHeader,
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get custom header",
            cancellationToken: cancellationToken
        );
    }
}