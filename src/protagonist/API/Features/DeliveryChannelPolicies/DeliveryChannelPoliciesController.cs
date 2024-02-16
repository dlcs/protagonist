using API.Features.DeliveryChannelPolicies.Converters;
using API.Features.DeliveryChannelPolicies.Requests;
using API.Features.DeliveryChannelPolicies.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
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
    private readonly string[] allowedDeliveryChannels =
    {
        AssetDeliveryChannels.Image,
        AssetDeliveryChannels.Timebased,
        AssetDeliveryChannels.Thumbnails
    };
    
    public DeliveryChannelPoliciesController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
        
    }
    
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollections(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
        if(!IsValidDeliveryChannelPolicy(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid delivery channel", null,
                400, "Invalid delivery channel policy");
        }
        
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "post"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        hydraDeliveryChannelPolicy.CustomerId = customerId;
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
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
        if(!IsValidDeliveryChannelPolicy(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid delivery channel", null,
                400, "Invalid delivery channel policy");
        }
        
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "put-patch"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        hydraDeliveryChannelPolicy.CustomerId = customerId;
        hydraDeliveryChannelPolicy.Name = deliveryChannelPolicyName;
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
        
        var updateDeliveryChannelPolicy =
            new UpdateDeliveryChannelPolicy(customerId, hydraDeliveryChannelPolicy.ToDlcsModel());
        
        return await HandleUpsert(updateDeliveryChannelPolicy, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update delivery channel policy",
            cancellationToken: cancellationToken);
    }
    
    [HttpPatch]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    public async Task<IActionResult> PatchDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        if(!IsValidDeliveryChannelPolicy(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid delivery channel", null,
                400, "Invalid delivery channel policy");
        }
        
        var validationResult = await validator.ValidateAsync(hydraDeliveryChannelPolicy, 
            policy => policy.IncludeRuleSets("default", "put-patch"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        hydraDeliveryChannelPolicy.CustomerId = customerId;
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
        hydraDeliveryChannelPolicy.Name = deliveryChannelPolicyName;
        
        var patchDeliveryChannelPolicy = new PatchDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName)
            {
                DisplayName = hydraDeliveryChannelPolicy.DisplayName,
                PolicyData = hydraDeliveryChannelPolicy.PolicyData
            };
        
        return await HandleUpsert(patchDeliveryChannelPolicy, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update delivery channel policy",
            cancellationToken: cancellationToken);    
    } 
    
    [HttpDelete]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    public async Task<IActionResult> DeleteDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName)
    {
        var deleteDeliveryChannelPolicy =
            new DeleteDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName);

        return await HandleDelete(deleteDeliveryChannelPolicy);
    }
    
    private bool IsValidDeliveryChannelPolicy(string deliveryChannelPolicyName)
    {
        return allowedDeliveryChannels.Contains(deliveryChannelPolicyName);
    }
}