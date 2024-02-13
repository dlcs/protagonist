
using API.Features.DeliveryChannelPolicies.Converters;
using API.Features.DeliveryChannelPolicies.Requests;
using API.Features.DeliveryChannelPolicies.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannelPolicies;

/// <summary>
/// DLCS REST API Operations for delivery channel policies
/// </summary>
[Route("/customers/{customerId}/deliveryChannelPolicies")]
[ApiController]
public class DeliveryChannelPoliciesController : HydraController
{
    public DeliveryChannelPoliciesController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
        
    }
    
    [HttpGet]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollections(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();     
    }

    [HttpGet("{channelName}")]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollection(
        [FromRoute] int customerId,
        [FromRoute] string channelId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }    
    
    [HttpPost("{channelName}")]
    public async Task<IActionResult> PostDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "post"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        throw new NotImplementedException();       
    }
    
    [HttpGet("{channelName}/{policyName}")]
    public async Task<IActionResult> GetDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelName,
        [FromRoute] string policyName,
        CancellationToken cancellationToken)
    { 
        var getDeliveryChannelPolicy = new GetDeliveryChannelPolicy(customerId, channelName, policyName );

        return await HandleFetch(
            getDeliveryChannelPolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get delivery channel policy failed",
            cancellationToken: cancellationToken
        );
    }
    
    [HttpPost("{channelName}/{policyName}")]
    public async Task<IActionResult> PutDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelName,
        [FromRoute] string policyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "put"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        throw new NotImplementedException();     
    }
    
    [HttpPatch("{channelId}/{policyName}")]
    public async Task<IActionResult> PatchDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelName,
        [FromRoute] string policyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "patch"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        throw new NotImplementedException();     
    } 
    
    [HttpDelete("{channelId}/{policyName}")]
    public async Task<IActionResult> DeleteDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelName,
        [FromRoute] string policyName,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    } 
}