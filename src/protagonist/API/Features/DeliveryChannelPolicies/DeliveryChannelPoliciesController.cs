
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

    [HttpGet]
    [Route("{deliveryChannelName}")]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollection(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }    
    
    [HttpPost]
    [Route("{deliveryChannelName}")]
    public async Task<IActionResult> PostDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName; // Model channel should be from path
        
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "post"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        hydraDeliveryChannelPolicy.CustomerId = customerId;
        var request = new CreateDeliveryChannelPolicy(customerId, hydraDeliveryChannelPolicy.ToDlcsModel());
        
        return await HandleUpsert(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create delivery channel policy",
            cancellationToken: cancellationToken);
    }
    
    [HttpGet]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    public async Task<IActionResult> GetDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        CancellationToken cancellationToken)
    { 
        var getDeliveryChannelPolicy =
            new GetDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName);

        return await HandleFetch(
            getDeliveryChannelPolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get delivery channel policy failed",
            cancellationToken: cancellationToken);
    }
    
    [HttpPut]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    public async Task<IActionResult> PutDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
        hydraDeliveryChannelPolicy.Name = deliveryChannelPolicyName;
        
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "put"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        var updateDeliveryChannelPolicy =
            new UpdateDeliveryChannelPolicy(customerId, hydraDeliveryChannelPolicy.ToDlcsModel());

        return await HandleUpsert(updateDeliveryChannelPolicy, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update delivery channel policy",
            cancellationToken: cancellationToken);
    }
    
    [HttpPatch]
    [Route("{deliveryChannelId}/{deliveryChannelPolicyName}")]
    public async Task<IActionResult> PatchDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
        hydraDeliveryChannelPolicy.Name = deliveryChannelPolicyName;
        
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "patch"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        throw new NotImplementedException();     
    } 
    
    [HttpDelete]
    [Route("{deliveryChannelId}/{deliveryChannelPolicyName}")]
    public async Task<IActionResult> DeleteDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName)
    {
        var deleteDeliveryChannelPolicy =
            new DeleteDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName);

        return await HandleDelete(deleteDeliveryChannelPolicy);
    } 
}