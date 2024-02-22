using System.Collections.Generic;
using System.Text.RegularExpressions;
using API.Features.DeliveryChannels.Converters;
using API.Features.DeliveryChannels.Requests;
using API.Features.DeliveryChannels.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Web.Requests;
using FluentValidation;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

/// <summary>
/// DLCS REST API Operations for delivery channel policies
/// </summary>
[Route("/customers/{customerId}/deliveryChannelPolicies")]
[ApiController]
public class DeliveryChannelPoliciesController : HydraController
{
    private readonly string[] allowedDeliveryChannels =
    {
        AssetDeliveryChannels.Thumbnails,
        AssetDeliveryChannels.Timebased,
    };
    
    public DeliveryChannelPoliciesController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
        
    }
    
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollections(
        [FromRoute] int customerId)
    {
        var baseUrl = Request.GetDisplayUrl(Request.Path);

        var hydraPolicyCollections = new List<HydraNestedCollection<DeliveryChannelPolicy>>()
        {
            new(baseUrl, AssetDeliveryChannels.Image)
            {
                Title = "Policies for IIIF Image service delivery",
            },
            new(baseUrl, AssetDeliveryChannels.Thumbnails)
            {
                Title = "Policies for thumbnails as IIIF Image Services",
            },
            new(baseUrl, AssetDeliveryChannels.Timebased)
            {
                Title = "Policies for Audio and Video delivery",
            },
            new(baseUrl, AssetDeliveryChannels.File)
            {
                Title = "Policies for File delivery",
            }
        };
        
        var result = new HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>()
        {
            WithContext = true,
            Members = hydraPolicyCollections.ToArray(),
            TotalItems = hydraPolicyCollections.Count,
            Id = Request.GetJsonLdId()
        };

        return new OkObjectResult(result);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("{deliveryChannelName}")]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollection(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        CancellationToken cancellationToken)
    {
        if (!IsValidDeliveryChannel(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid delivery channel", null,
                400, "Invalid delivery channel");
        }

        var request = new GetDeliveryChannelPolicies(customerId, deliveryChannelName);
        
        return await HandleListFetch<DLCS.Model.Policies.DeliveryChannelPolicy, GetDeliveryChannelPolicies, DeliveryChannelPolicy>(
            request,
            p => p.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get delivery channel policies",
            cancellationToken: cancellationToken
        );
    }    
    
    [HttpPost]
    [Route("{deliveryChannelName}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        if(!IsPermittedDeliveryChannel(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid/permitted delivery channel", null,
                400, "Invalid delivery channel policy");
        }
        
        if (!IsValidName(hydraDeliveryChannelPolicy.Name))
        {
            return this.HydraProblem($"'The name specified for this delivery channel policy is invalid", null,
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        if(!IsPermittedDeliveryChannel(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid/permitted delivery channel", null,
                400, "Invalid delivery channel policy");
        }
      
        if (!IsValidName(deliveryChannelPolicyName))
        {
            return this.HydraProblem($"'The name specified for this delivery channel policy is invalid", null,
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        [FromServices] HydraDeliveryChannelPolicyValidator validator,
        CancellationToken cancellationToken)
    {
        if(!IsPermittedDeliveryChannel(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid/permitted delivery channel", null,
                400, "Invalid delivery channel policy");
        }
        
        if (!IsValidName(deliveryChannelPolicyName))
        {
            return this.HydraProblem($"The name specified for this delivery channel policy is invalid", null,
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
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName)
    {
        if(!IsPermittedDeliveryChannel(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid/permitted delivery channel", null,
                400, "Invalid delivery channel policy");
        }
        
        var deleteDeliveryChannelPolicy =
            new DeleteDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName);

        return await HandleDelete(deleteDeliveryChannelPolicy);
    }
    
    private bool IsValidDeliveryChannel(string deliveryChannelPolicyName)
    {
        return AssetDeliveryChannels.All.Contains(deliveryChannelPolicyName);
    }
    
    private bool IsPermittedDeliveryChannel(string deliveryChannelPolicyName)
    {
        return allowedDeliveryChannels.Contains(deliveryChannelPolicyName);
    }
    
    private bool IsValidName(string? inputName)
    {
        const string regex = "[\\sA-Z]"; // Delivery channel policy names should not contain capital letters or spaces
        return !(string.IsNullOrEmpty(inputName) || Regex.IsMatch(inputName, regex));
    }
}