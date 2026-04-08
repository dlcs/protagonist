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
using Microsoft.Extensions.Options;

namespace API.Features.Adjuncts;

/// <summary>
/// Controller for handling requests for adjunct resources
/// </summary>
[Route("/customers/{customerId}/spaces/{spaceId}/images/{imageId}/adjuncts")]
[ApiController]
public class AdjunctsController(
    IOptions<ApiSettings> options,
    IMediator mediator)
    : HydraController(options.Value, mediator)
{
    /// <summary>
    /// Get details of all adjuncts for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD collection of Adjunct objects, representing the adjuncts.</returns>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> GetAllAdjuncts(int customerId, int spaceId, string imageId, CancellationToken cancellationToken)
    {
        var getAdjuncts = new GetAllAdjuncts(new AssetId(customerId, spaceId, imageId));

        return await HandleListFetch<DLCS.Model.Assets.Adjunct, GetAllAdjuncts, Adjunct>(
            getAdjuncts,
            policy => policy.ToHydra(GetUrlRoots()),
            errorTitle: "Get adjuncts failed",
            cancellationToken: cancellationToken
        );
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
    /// Create an adjunct, or adjuncts, for an asset.
    /// </summary>
    /// <returns>A Hydra JSON-LD Adjunct object representing the adjunct, or a HydraCollection if more than 1 submitted</returns>
    [HttpPost]
    [ProducesResponseType(200, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(201, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(200, Type = typeof(Adjunct))]
    [ProducesResponseType(201, Type = typeof(Adjunct))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> PostAdjunct(int customerId, int spaceId, string imageId, 
        [FromBody] FlexCollection<Adjunct> adjuncts, 
        [FromServices] HydraAdjunctValidator validator, CancellationToken cancellationToken = default)
    {
        if (adjuncts.Items is not { Length: > 0 } hydraAdjuncts)
        {
            return this.HydraProblem($"One or more adjuncts were expected in request body but found none", null, 400);
        }
        
        return await CreateOrUpdateAdjunct(customerId, spaceId, imageId, hydraAdjuncts, validator, true, cancellationToken);
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
        if (!string.IsNullOrEmpty(hydraAdjunct.ModelId) && adjunctId != hydraAdjunct.ModelId)
        {
            return this.HydraProblem($"The adjunct id from the request URI ({adjunctId}) does not match the 'id' from the request body ({hydraAdjunct.ModelId})",
                null, 400);
        }
        
        hydraAdjunct.ModelId = adjunctId;
        
        return await CreateOrUpdateAdjunct(customerId, spaceId, imageId, [hydraAdjunct], validator, false, cancellationToken);
    }

    /// <summary>
    /// Delete an adjunct for an asset.
    /// </summary>
    /// <returns>A delete result.</returns>
    [HttpDelete("{adjunctId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> DeleteAdjunct(int customerId, int spaceId, string imageId, string adjunctId, [FromQuery] string? deleteFrom)
    {
        var additionalDeletion = ImageCacheTypeConverter.ConvertToImageCacheType(deleteFrom, ',');
        var deleteRequest = new DeleteAdjunct(adjunctId, new AssetId(customerId, spaceId, imageId),  additionalDeletion);

        return await HandleDelete(deleteRequest);
    }

    private async Task<IActionResult> CreateOrUpdateAdjunct(int customerId, int spaceId, string imageId, Adjunct hydraAdjunct,
    
    private async Task<IActionResult> CreateOrUpdateAdjunct(int customerId, int spaceId, string imageId, Adjunct[] hydraAdjuncts,
        HydraAdjunctValidator validator, bool createOnly, CancellationToken cancellationToken)
    {
        foreach (var hydraAdjunct in hydraAdjuncts)
        {
            var validationResult = await validator.ValidateAsync(hydraAdjunct, cancellationToken);
            if (!validationResult.IsValid)
            {
                return this.ValidationFailed(validationResult);
            }
        }

        var dlcsAdjuncts = hydraAdjuncts
            .Select(a => a.ToDlcsModel(customerId, spaceId, imageId))
            .ToArray();
        
        var createOrUpdateRequest =
            new CreateOrUpdateAdjunct(dlcsAdjuncts, createOnly);
        
        return await HandleUpsert(
            createOrUpdateRequest,
            a => a.ToHydra(GetUrlRoots()),
            new AssetId(customerId, spaceId, imageId).ToString(),
            "Create or update adjunct failed", cancellationToken);
    }
}
