using API.Features.OriginStrategies.Converters;
using API.Features.OriginStrategies.Requests;
using API.Features.OriginStrategies.Validators;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Enum;
using DLCS.Core.Strings;
using DLCS.Model.Customers;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CustomerOriginStrategy = DLCS.HydraModel.CustomerOriginStrategy;

namespace API.Features.OriginStrategies;

/// <summary>
/// DLCS REST API operations for customer origin strategies
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
    /// Get the user's origin strategies
    /// </summary>
    /// <returns>HydraCollection of CustomerOriginStrategies</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerOriginStrategies(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var strategies = new GetAllCustomerOriginStrategies(customerId);
        
        return await HandleListFetch<DLCS.Model.Customers.CustomerOriginStrategy, 
            GetAllCustomerOriginStrategies, CustomerOriginStrategy>(
            strategies,
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get origin strategies",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Update an origin strategy owned by the user
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///     POST: /customers/1/originStrategies
    ///     {
    ///          "regex": "http[s]?://(.*).my-regex.com",
    ///          "order": "1",
    ///          "strategy": "basic-http-authentication", 
    ///          "credentials": "{ \"user\": \"my-username\", \"password\": \"my-password\" }",
    ///          "optimised": "false"
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
        var validationResult = await validator.ValidateAsync(newStrategy, 
            strategy => strategy.IncludeRuleSets("default", "create"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
     
        if (GetAllowedStrategy(newStrategy.OriginStrategy!) == null)
            return this.HydraProblem($"'{newStrategy.OriginStrategy}' is not allowed as an origin strategy type", null,
                400, "Invalid origin strategy");
        
        newStrategy.CustomerId = customerId;
        var request = new CreateCustomerOriginStrategy(customerId, newStrategy.ToDlcsModel());
        
        return await HandleUpsert(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create origin strategy",
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
            errorTitle: "Failed to get origin strategy",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Update an origin strategy owned by the user
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///     PUT: /customers/1/originStrategies/68a8931b-e815-492b-bfe9-2f8135ba4898
    ///     {
    ///          "regex": "http[s]?://(.*).my-regex.com",
    ///          "order": "1",
    ///          "strategy": "basic-http-authentication", 
    ///          "credentials": "{ \"user\": \"my-username\", \"password\": \"my-password\" }",
    ///          "optimised": "false"
    ///     }
    /// </remarks>
    [HttpPut]
    [Route("{strategyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutCustomerOriginStrategy(
        [FromRoute] int customerId,
        [FromRoute] string strategyId,
        [FromBody] CustomerOriginStrategy strategyChanges,
        [FromServices] HydraCustomerOriginStrategyValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(strategyChanges, 
            strategy => strategy.IncludeRuleSets("default"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        OriginStrategyType? strategyType = null;
        
        if (strategyChanges.OriginStrategy.HasText())
        {
            strategyType = GetAllowedStrategy(strategyChanges.OriginStrategy);
            if(strategyType == null)
                return this.HydraProblem($"'{strategyChanges.OriginStrategy}' is not allowed as an origin strategy type", null, 
                    400, "Invalid origin strategy");
        } 

        var request = new UpdateCustomerOriginStrategy(customerId, strategyId)
        {
            Regex = strategyChanges.Regex,
            Strategy = strategyType,
            Credentials = strategyChanges.Credentials,
            Order = strategyChanges.Order,
            Optimised = strategyChanges.Optimised
        };
        
        return await HandleUpsert(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update origin strategy",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Delete a specified origin strategy owned by the user
    /// </summary>
    [HttpDelete]
    [Route("{strategyId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomerOriginStrategy(
        [FromRoute] int customerId,
        [FromRoute] string strategyId)
    {
        var deleteRequest = new DeleteCustomerOriginStrategy(customerId, strategyId);

        return await HandleDelete(deleteRequest);
    }

    private OriginStrategyType? GetAllowedStrategy(string strategy)
    {
        var strategyType = strategy.GetEnumFromString<OriginStrategyType>();
        return allowedStrategyTypes.Contains(strategyType) ? strategyType : null;
    }
}