using API.Converters;
using API.Features.Adjuncts.Requests;
using API.Features.Adjuncts.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using FluentValidation;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Adjuncts;

/// <summary>
/// Controller for handling requests for adjunct resources
/// </summary>
[Route("/customers/{customerId}/spaces/{spaceId}/images/{imageId}/adjuncts")]
[ApiController]
public class AdjunctsController(
    IOptions<ApiSettings> options,
    IMediator mediator,
    ILogger<AdjunctsController> logger)
    : HydraController(options.Value, mediator)
{
    /// <summary>
    /// Get details of all adjuncts for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Image object representing the adjuncts.</returns>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> GetAdjuncts(int customerId, int spaceId, string imageId)
    {
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// Get details of an adjunct.
    /// </summary>
    /// <returns>A Hydra JSON-LD Image object representing the adjunct.</returns>
    [HttpGet("{adjunctId}")]
    [ProducesResponseType(200, Type = typeof(Adjunct))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> GetAdjunct(int customerId, int spaceId, string imageId, string adjunctId, CancellationToken cancellationToken)
    {
        var getAdjunct = new GetAdjunct(adjunctId, new AssetId(customerId, spaceId, imageId));

        return await HandleFetch(
            getAdjunct,
            adjunct => adjunct.ToHydra(GetUrlRoots()),
            errorTitle: "Get adjunct failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Create an adjunct for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Image object representing the adjuncts.</returns>
    [HttpPost]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> PostAdjunct(int customerId, int spaceId, string imageId, 
        [FromBody] Adjunct hydraAdjunct, 
        [FromServices] HydraAdjunctValidator validator, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(hydraAdjunct, 
            strategy => strategy.IncludeRuleSets("default", "create"), cancellationToken); //todo: combine with put?
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        var createOrUpdateRequest = new CreateOrUpdateAdjunct(hydraAdjunct.ToDlcsModel(customerId, spaceId, imageId));

        return await HandleUpsert(
            createOrUpdateRequest,
            a => a.ToHydra(GetUrlRoots()),
            createOrUpdateRequest.Adjunct.Id,
            "POST adjunct failed", cancellationToken);
    }
    
    /// <summary>
    /// Create or update an adjunct for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Image object representing the adjuncts.</returns>
    [HttpPut("{adjunctId}")]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> PutAdjunct(int customerId, int spaceId, string imageId, string adjunctId)
    {
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// Delete an adjunct for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Image object representing the adjuncts.</returns>
    [HttpDelete("{adjunctId}")]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> DeleteAdjunct(int customerId, int spaceId, string imageId, string adjunctId)
    {
        throw new NotImplementedException();
    }
}
