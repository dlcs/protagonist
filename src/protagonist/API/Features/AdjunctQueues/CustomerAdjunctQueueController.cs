using API.Converters;
using API.Features.AdjunctQueues.Converters;
using API.Features.AdjunctQueues.Requests;
using API.Features.AdjunctQueues.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.AdjunctQueues;

/// <summary>
/// Controller for handling requests relating to adjunct queue batches
/// </summary>
[Route("/customers/{customerId}/adjunctQueue")]
[ApiController]
public class CustomerAdjunctQueueController(
    IOptions<ApiSettings> options,
    IMediator mediator)
    : HydraController(options.Value, mediator)
{
    /// <summary>
    /// Submit a batch of adjuncts to the adjunct queue.
    /// </summary>
    /// <returns>A Hydra JSON-LD AdjunctBatch object representing the created batch.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AdjunctBatch))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    public async Task<IActionResult> CreateAdjunctBatch(
        [FromRoute] int customerId,
        [FromBody] HydraCollection<Adjunct> adjuncts,
        [FromServices] AdjunctBatchPostValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(adjuncts, cancellationToken);
        if (!validationResult.IsValid) return this.ValidationFailed(validationResult);

        return await HandleHydraRequest(() =>
        {
            // Convert at controller boundary; ToDlcsModel(customerId) throws BadRequestException
            // for invalid/mismatched asset refs
            var dlcsAdjuncts = adjuncts.Members!
                .Select(a => a.ToDlcsModel(customerId))
                .ToArray();

            return HandleUpsert(
                new CreateAdjunctBatch(customerId, dlcsAdjuncts),
                batch => batch.ToHydra(GetUrlRoots().BaseUrl),
                errorTitle: "Create adjunct batch failed",
                cancellationToken: cancellationToken);
        });
    }
}
