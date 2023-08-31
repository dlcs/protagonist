using API.Features.CustomHeaders.Converters;
using API.Features.CustomHeaders.Requests;
using API.Features.CustomHeaders.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.CustomHeaders;

/// <summary>
/// DLCS REST API Operations for Custom Headers
/// </summary>
[Route("/customers/{customerId}/customHeaders")]
[ApiController]
public class CustomHeadersController : HydraController
{
    public CustomHeadersController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get a list of Custom Headers owned by the calling user
    /// </summary>
    /// <returns>HydraCollection of CustomHeader</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomHeaders(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var customHeaders = new GetAllCustomHeaders(customerId);

        return await HandleListFetch<DLCS.Model.Assets.CustomHeaders.CustomHeader, GetAllCustomHeaders, CustomHeader>(
            customHeaders,
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get Custom Headers",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Create a new Custom Header owned by the calling user
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/customHeaders
    ///     {
    ///         "key": "my-key",
    ///         "value": "my-value"
    ///         (optional) "space": 1
    ///         (optional) "role": "https://api.dlcs.digirati.io/customers/1/roles/my-role"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostCustomHeader(
        [FromRoute] int customerId,
        [FromBody] CustomHeader newCustomHeader,
        [FromServices] HydraCustomHeaderValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(newCustomHeader, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        newCustomHeader.CustomerId = customerId;
        var request = new CreateCustomHeader(customerId, newCustomHeader.ToDlcsModel());
        
        return await HandleUpsert(request, 
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create Custom Header",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Get a specified Custom Header owned by the calling user
    /// </summary>
    [HttpGet]
    [Route("{customHeaderId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomHeader(
        [FromRoute] int customerId,
        [FromRoute] string customHeaderId,
        CancellationToken cancellationToken)
    {
        var customHeader = new GetCustomHeader(customerId, customHeaderId);
        
        return await HandleFetch(
            customHeader,
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get Custom Header",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Update an existing Custom Header owned by the calling user
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT: /customers/1/customHeaders/3abc55fd-eb2d-47e8-8966-5f71d8e26476
    ///     {
    ///         "key": "my-new-key",
    ///         "value": "my-new-value"
    ///         (optional) "space": 2
    ///         (optional) "role": "https://api.dlcs.digirati.io/customers/1/roles/my-new-role"
    ///     }
    /// </remarks>
    [HttpPut]
    [Route("{customHeaderId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutCustomHeader(
        [FromRoute] int customerId,
        [FromRoute] string customHeaderId,
        [FromBody] CustomHeader customHeaderChanges,
        [FromServices] HydraCustomHeaderValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(customHeaderChanges, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        customHeaderChanges.CustomerId = customerId;
        customHeaderChanges.ModelId = customHeaderId;
        var request = new UpdateCustomHeader(customerId, customHeaderChanges.ToDlcsModel());
        
        return await HandleUpsert(request, 
            ch => ch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update Custom Header",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Delete a specified Custom Header owned by the calling user
    /// </summary>
    [HttpDelete]
    [Route("{customHeaderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomHeader(
        [FromRoute] int customerId,
        [FromRoute] string customHeaderId)
    {
        var deleteRequest = new DeleteCustomHeader(customerId, customHeaderId);

        return await HandleDelete(deleteRequest);
    }
}