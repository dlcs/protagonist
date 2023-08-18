using API.Features.OriginStrategies.Converters;
using API.Features.OriginStrategies.Requests;
using API.Features.OriginStrategies.Validators;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Enum;
using DLCS.Core.Strings;
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
    private readonly OriginStrategyType[] allowedStrategyTypes = 
        { OriginStrategyType.BasicHttp, OriginStrategyType.S3Ambient };
    
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
     
        if (IsStrategyAllowed(newStrategy.OriginStrategy!) == null)
            return this.HydraProblem($"'{newStrategy.OriginStrategy}' is not a valid origin strategy", null, 400, "Invalid Origin Strategy");
        
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
        OriginStrategyType? strategyType = null;
        
        if (strategy.OriginStrategy.HasText())
        {
            strategyType = IsStrategyAllowed(strategy.OriginStrategy);
            if(strategyType == null)
                return this.HydraProblem($"'{strategy.OriginStrategy}' is not a valid origin strategy", null, 400, "Invalid Origin Strategy");
        } 

        var request = new UpdateCustomerOriginStrategy(customerId, strategyId)
        {
            Regex = strategy.Regex,
            Strategy = strategyType,
            Credentials = strategy.Credentials,
            Order = strategy.Order,
            Optimised = strategy.Optimised
        };
        
        return await HandleUpsert(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update Origin Strategy",
            cancellationToken: cancellationToken);
    }
    
    private OriginStrategyType? IsStrategyAllowed(string strategy)
    {
        var strategyType = strategy.GetEnumFromString<OriginStrategyType>();
        if (allowedStrategyTypes.Contains(strategyType)) return strategyType;
        return null;
    }
}