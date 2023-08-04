using API.Features.OriginStrategies.Converters;
using API.Features.OriginStrategies.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.OriginStrategies;

/// <summary>
/// Controller for handling requests for origin strategies
/// </summary>
[Route("/customers/{customerId}/originStrategies")]
[ApiController]
public class CustomerOriginStrategiesController : HydraController
{
    public CustomerOriginStrategiesController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get a list of the user's origin strategies
    /// </summary>
    /// <returns>HydraCollection of CustomerOriginStrategies</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerOriginStrategies(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var strategies = new GetAllCustomerOriginStrategies(customerId);
        
        return await HandleListFetch<DLCS.Model.Customers.CustomerOriginStrategy, 
            GetAllCustomerOriginStrategies, CustomerOriginStrategy>(
            strategies,
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get Origin Strategies",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get a specified origin strategy owned by the user
    /// </summary>
    [HttpGet]
    [Route("{strategyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerOriginStrategy(
        [FromRoute] int customerId,
        [FromRoute] string strategyId,
        CancellationToken cancellationToken)
    {
        var strategy = new GetCustomerOriginStrategy(customerId, strategyId);
        
        return await HandleFetch(
            strategy,
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get Origin Strategy",
            cancellationToken: cancellationToken
        );
    }
}