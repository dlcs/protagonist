
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
    public async Task<IActionResult> GetPolicyCollections(int customerId)
    {
        throw new NotImplementedException();     
    }

    [HttpGet("{channelId}")]
    public async Task<IActionResult> GetPolicyCollection(int customerId, string channelId)
    {
        throw new NotImplementedException();
    }    
    
    [HttpPost("{channelId}")]
    public async Task<IActionResult> PostPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelId,
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
    
    [HttpGet("{channelId}/{policyId}")]
    [ProducesResponseType(200, Type = typeof(DLCS.HydraModel.DeliveryChannelPolicy))]
    public async Task<IActionResult> GetPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelId,
        [FromRoute] string policyId)
    { 
        throw new NotImplementedException();
    }
    
    [HttpPost("{channelId}/{policyId}")]
    public async Task<IActionResult> PutPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelId,
        [FromRoute] string policyId,
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
    
    [HttpPatch("{channelId}/{policyId}")]
    public async Task<IActionResult> PatchPolicy(
        [FromRoute] int customerId,
        [FromRoute] string channelId,
        [FromRoute] string policyId,
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
    
    [HttpDelete("{channelId}/{policyId}")]
    public async Task<IActionResult> DeletePolicy(int customerId, string channelId, string policyId)
    {
        throw new NotImplementedException();
    } 
}