using API.Converters;
using API.Features.Adjuncts.Requests;
using API.Features.Adjuncts.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Types;
using DLCS.HydraModel;
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
    /// <returns>A Hydra JSON-LD Adjunct object representing the adjuncts.</returns>
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
    /// <returns>A Hydra JSON-LD Adjunct object representing the adjunct.</returns>
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
    /// <returns>A Hydra JSON-LD Adjunct object representing the adjuncts.</returns>
    [HttpPost]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> PostAdjunct(int customerId, int spaceId, string imageId, 
        [FromBody] Adjunct hydraAdjunct, 
        [FromServices] HydraAdjunctValidator validator, CancellationToken cancellationToken = default)
    {
        return await CreateOrUpdateAdjunct(customerId, spaceId, imageId, hydraAdjunct, validator, true, cancellationToken);
    }

    /// <summary>
    /// Create or update an adjunct for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Adjunct object representing the adjuncts.</returns>
    [HttpPut("{adjunctId}")]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> PutAdjunct(int customerId, int spaceId, string imageId, string adjunctId, [FromBody] Adjunct hydraAdjunct, 
        [FromServices] HydraAdjunctValidator validator, CancellationToken cancellationToken = default)
    {
        if (hydraAdjunct.ModelId != null && adjunctId != hydraAdjunct.ModelId)
        {
            return this.HydraProblem($"The adjunct id from the request URI does not match the 'id' from the request body",
                null, 400);
        }
        
        hydraAdjunct.ModelId = adjunctId;
        
        return await CreateOrUpdateAdjunct(customerId, spaceId, imageId, hydraAdjunct, validator, false, cancellationToken);
    }
    
    private async Task<IActionResult> CreateOrUpdateAdjunct(int customerId, int spaceId, string imageId, Adjunct hydraAdjunct,
        HydraAdjunctValidator validator, bool createOnly, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(hydraAdjunct, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var createOrUpdateRequest =
            new CreateOrUpdateAdjunct(hydraAdjunct.ToDlcsModel(customerId, spaceId, imageId), createOnly);

        return await HandleUpsert(
            createOrUpdateRequest,
            a => a.ToHydra(GetUrlRoots()),
            createOrUpdateRequest.Adjunct.Id,
             "Create or update adjunct failed", cancellationToken);
    }

    /// <summary>
    /// Delete an adjunct for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Adjunct object representing the adjuncts.</returns>
    [HttpDelete("{adjunctId}")]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> DeleteAdjunct(int customerId, int spaceId, string imageId, string adjunctId)
    {
        throw new NotImplementedException();
    }
}
