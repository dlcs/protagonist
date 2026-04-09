using API.Converters;
using API.Features.Customer.Infrastructure;
using API.Features.Customer.Requests;
using API.Features.Customer.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Model;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Customer;

/// <summary>
/// Controller for handling bulk requests for adjuncts associated with a customer 
/// </summary>
[Route("/customers/{customerId}")]
public class CustomerAdjunctsController(IOptions<ApiSettings> settings, IMediator mediator)
    : HydraController(settings.Value, mediator)
{
    /// <summary>
    /// Accepts a list of image identifiers, will delete those that exist from DB
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST: /customers/1/deleteAdjuncts
    ///     {
    ///         "@context": "http://www.w3.org/ns/hydra/context.jsonld",
    ///         "@type":"Collection",
    ///         "member": [
    ///             { "id": "1/1/foo", "adjunct": ["ocr.txt", "mets.xml"] }
    ///         ]
    ///     }
    /// </remarks>
    [HttpPost]
    [Route("deleteAdjuncts")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> DeleteAdjuncts(
        [FromRoute] int customerId,
        [FromQuery] string? deleteFrom,
        [FromBody] HydraCollection<AdjunctAssetIdentifier> adjunctIdentifiers,
        [FromServices] AdjunctIdListValidator validator,
        CancellationToken cancellationToken = default)
    {
        var adjunctIdentifiersDictionary = adjunctIdentifiers.Members?.ConvertToDictionary();
        
        var validationResult = await validator.ValidateAsync(adjunctIdentifiersDictionary, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var additionalDeletion = ImageCacheTypeConverter.ConvertToImageCacheType(deleteFrom, ',');

        return await HandleHydraRequest(async () =>
        {
            var deleteRequest =
                new DeleteMultipleAdjunctsById(adjunctIdentifiersDictionary,
                    customerId, additionalDeletion);
            var deletedRows = await Mediator.Send(deleteRequest, cancellationToken);

            if (deletedRows == 0)
            {
                return this.HydraProblem("No adjuncts found", null, 400, "Delete adjuncts failed");
            }

            return NoContent();
        }, "Delete adjuncts failed");
    }
}
