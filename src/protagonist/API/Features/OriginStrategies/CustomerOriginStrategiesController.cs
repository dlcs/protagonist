using API.Features.OriginStrategies.Converters;
using API.Features.OriginStrategies.Requests;
using API.Features.OriginStrategies.Validators;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Enum;
using DLCS.Model.Customers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CustomerOriginStrategy = DLCS.HydraModel.CustomerOriginStrategy;

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
    /// Create a new origin strategy owned by the user
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/originStrategies
    ///     {
    ///          "originStrategy":"basic-http-authentication",
    ///          "regex": "your-regex-here"
    ///          "optimised": "false",
    ///          "credentials": "{"user": "user-example", "password": "password-example"}"
    ///          "order": 2
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostCustomerOriginStrategy(
        [FromRoute] int customerId,
        [FromBody] CustomerOriginStrategy newStrategy,
        [FromServices] HydraCustomerOriginStrategyValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(newStrategy, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        newStrategy.CustomerId = customerId;
        var request = new CreateCustomerOriginStrategy(customerId, newStrategy.ToDlcsModel());
        return await HandleUpsert(request, 
            os => os.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create Origin Strategy",
            cancellationToken: cancellationToken);
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
    
    [HttpPut]
    [Route("{strategyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutCustomerOriginStrategy(
        [FromRoute] int customerId,
        [FromRoute] string strategyId,
        [FromBody] CustomerOriginStrategy strategy,
        [FromServices] HydraCustomerOriginStrategyValidator validator,
        CancellationToken cancellationToken)
    {
        var request = new UpdateCustomerOriginStrategy(customerId, strategyId)
        {
            Regex = strategy.Regex,
            Strategy = strategy.OriginStrategy,
            Order = strategy.Order,
            Optimised = strategy.Optimised
        };
        
        return await HandleUpsert(request, 
            os => os.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update Origin Strategy",
            cancellationToken: cancellationToken);
    }
}