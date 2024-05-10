using API.Exceptions;
using API.Features.DeliveryChannels.Converters;
using API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;
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
    private readonly HydraDeliveryChannelPolicyValidator hydraDeliveryChannelPolicyValidator;
    
    public DeliveryChannelPoliciesController(
        IMediator mediator,
        IOptions<ApiSettings> options,
        HydraDeliveryChannelPolicyValidator hydraDeliveryChannelPolicyValidator) : base(options.Value, mediator)
    {
        this.hydraDeliveryChannelPolicyValidator = hydraDeliveryChannelPolicyValidator;
    }
    
    /// <summary>
    /// Get a collection of nested DeliveryChannelPolicy collections, sorted by channel
    /// </summary>
    /// <returns>HydraCollection of DeliveryChannelPolicy HydraCollection</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollections(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var request = new GetDeliveryChannelPolicyCollections(Request.GetDisplayUrl(Request.Path), Request.GetJsonLdId());
        var result = await Mediator.Send(request, cancellationToken);
        var policyCollections = new HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>()
        {
            WithContext = true,
            Members = result.Select(c => 
                new HydraNestedCollection<DeliveryChannelPolicy>(request.BaseUrl, c.Key)
                {
                    Title = c.Value,
                }).ToArray(),
            TotalItems = result.Count,
            Id = request.JsonLdId,
        }; 
        
        return Ok(policyCollections);
    }

    /// <summary>
    /// Get a collection of the customer's delivery channel policies for a specific channel
    /// </summary>
    /// <returns>HydraCollection of DeliveryChannelPolicy</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("{deliveryChannelName}")]
    public async Task<IActionResult> GetDeliveryChannelPolicyCollection(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        CancellationToken cancellationToken)
    {
        if (!AssetDeliveryChannels.IsValidChannel(deliveryChannelName))
        {
            return this.HydraProblem($"'{deliveryChannelName}' is not a valid delivery channel", null,
                400, "Invalid delivery channel");
        }

        var request = new GetPoliciesForDeliveryChannel(customerId, deliveryChannelName);
        
        return await HandleListFetch<DLCS.Model.Policies.DeliveryChannelPolicy, GetPoliciesForDeliveryChannel, DeliveryChannelPolicy>(
            request,
            p => p.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get delivery channel policies",
            cancellationToken: cancellationToken
        );
    }    
    
    /// <summary>
    /// Create a new policy for a specified delivery channel
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/deliveryChannelPolicies/iiif-av
    ///     {
    ///         "name": "my-video-policy"
    ///         "displayName": "My Video Policy",
    ///         "policyData": "["video-mp4-720p"]"
    ///     }
    /// </remarks>
    [HttpPost]
    [Route("{deliveryChannelName}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        CancellationToken cancellationToken)
    {
        const string errorMessage = "Failed to create delivery channel policy";
        
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
        
        var validateResult = await TryValidateHydraDeliveryChannelPolicy(hydraDeliveryChannelPolicy, errorMessage,
            new[]{ "default", "post" }, cancellationToken);
        if (validateResult is not OkResult)
        {
            return validateResult;
        }

        hydraDeliveryChannelPolicy.CustomerId = customerId;
        var request = new CreateDeliveryChannelPolicy(customerId, hydraDeliveryChannelPolicy.ToDlcsModel());
        
        return await HandleUpsert(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: errorMessage,
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Get a delivery channel policy belonging to a customer
    /// </summary>
    /// <returns>DeliveryChannelPolicy</returns>
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
    
    /// <summary>
    /// Create or update a specified customer delivery channel policy - "name" must be specified in URI
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT: /customers/1/deliveryChannelPolicies/iiif-av/my-video-policy
    ///     {
    ///         "displayName": "My Updated Video Policy",
    ///         "policyData": "["video-mp4-720p"]"
    ///     }
    /// </remarks>
    /// <returns>DeliveryChannelPolicy</returns>
    [HttpPut]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PutDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        CancellationToken cancellationToken)
    {
        const string errorMessage = "Failed to update delivery channel policy";
        
        hydraDeliveryChannelPolicy.Name = deliveryChannelPolicyName;
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;

        var validateResult = await TryValidateHydraDeliveryChannelPolicy(hydraDeliveryChannelPolicy, errorMessage,
            new[]{ "default", "put" }, cancellationToken);
        if (validateResult is not OkResult)
        {
            return validateResult;
        }

        hydraDeliveryChannelPolicy.CustomerId = customerId;
        
        var updateDeliveryChannelPolicy =
            new UpsertDeliveryChannelPolicy(customerId, hydraDeliveryChannelPolicy.ToDlcsModel());
        
        return await HandleUpsert(updateDeliveryChannelPolicy, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: errorMessage,
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Update the supplied fields for a specified customer delivery channel policy
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PATCH: /customers/1/deliveryChannelPolicies/iiif-av/my-video-policy
    ///     {
    ///         "displayName": "My Updated Video Policy"
    ///     }
    /// </remarks>
    /// <returns>DeliveryChannelPolicy</returns>
    [HttpPatch]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        [FromBody] DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        CancellationToken cancellationToken)
    {
        const string errorMessage = "Failed to patch delivery channel policy";
        
        hydraDeliveryChannelPolicy.Channel = deliveryChannelName;
        hydraDeliveryChannelPolicy.Name = deliveryChannelPolicyName;

        var validateResult = await TryValidateHydraDeliveryChannelPolicy(hydraDeliveryChannelPolicy, errorMessage,
            new[]{ "default", "patch" }, cancellationToken);
        if (validateResult is not OkResult)
        {
            return validateResult;
        }
        
        hydraDeliveryChannelPolicy.CustomerId = customerId;

        var patchDeliveryChannelPolicy =
            new PatchDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName,
                hydraDeliveryChannelPolicy.DisplayName, hydraDeliveryChannelPolicy.PolicyData);
        
        return await HandleUpsert(patchDeliveryChannelPolicy, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: errorMessage,
            cancellationToken: cancellationToken);    
    } 
        
    /// <summary>
    /// Delete a specified delivery channel policy
    /// </summary>
    [HttpDelete]
    [Route("{deliveryChannelName}/{deliveryChannelPolicyName}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDeliveryChannelPolicy(
        [FromRoute] int customerId,
        [FromRoute] string deliveryChannelName,
        [FromRoute] string deliveryChannelPolicyName,
        CancellationToken cancellationToken)
    {
        var deleteDeliveryChannelPolicy =
            new DeleteDeliveryChannelPolicy(customerId, deliveryChannelName, deliveryChannelPolicyName);

        return await HandleDelete(
            deleteDeliveryChannelPolicy,
            errorTitle: "Delete delivery channel policy failed",
            cancellationToken);
    }

    private async Task<IActionResult> TryValidateHydraDeliveryChannelPolicy(
        DeliveryChannelPolicy hydraDeliveryChannelPolicy,
        string apiErrorMessage,
        string[] validatorRuleSets,
        CancellationToken cancellationToken)
    {
        try
        {
            var hydraDeliveryChannelValidationResult = await hydraDeliveryChannelPolicyValidator.ValidateAsync(
                hydraDeliveryChannelPolicy,
                policy => policy.IncludeRuleSets(validatorRuleSets), cancellationToken);
            if (!hydraDeliveryChannelValidationResult.IsValid)
            {
                return this.ValidationFailed(hydraDeliveryChannelValidationResult);
            }
        }
        catch(APIException apiEx)
        {
            return this.HydraProblem(apiEx.Message, null, apiEx.StatusCode, apiErrorMessage);
        }

        return Ok();
    }
}