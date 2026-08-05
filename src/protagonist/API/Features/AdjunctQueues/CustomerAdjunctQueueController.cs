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
[Route("/customers/{customerId:int}/adjunctQueue")]
[ApiController]
public class CustomerAdjunctQueueController(
    IOptions<ApiSettings> options,
    IMediator mediator)
    : HydraController(options.Value, mediator)
{
    /// <summary>
    /// Get details of default customer adjunct queue
    /// </summary>
    /// <param name="customerId">Id of customer to get adjunct queue details for</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD CustomerAdjunctQueue object</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerAdjunctQueue))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetCustomerAdjunctQueue([FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        return await HandleFetch(
            new GetCustomerAdjunctQueue(customerId),
            queue => queue.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get Customer Adjunct Queue failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of all customer adjunct batches.
    ///
    /// Supports ?page= and ?pageSize= query parameters for paging
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD collection of AdjunctBatch objects</returns>
    [HttpGet]
    [Route("batches")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<AdjunctBatch>))]
    public async Task<IActionResult> GetAdjunctBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        return await HandlePagedFetch<DLCS.Model.Assets.AdjunctBatch, GetAdjunctBatches, AdjunctBatch>(
            new GetAdjunctBatches(customerId),
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get adjunct batches failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of customer active adjunct batches. An "active" batch is one that is incomplete.
    ///
    /// Supports ?page= and ?pageSize= query parameters for paging
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD collection of AdjunctBatch objects</returns>
    [HttpGet]
    [Route("active")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<AdjunctBatch>))]
    public async Task<IActionResult> GetActiveAdjunctBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        return await HandlePagedFetch<DLCS.Model.Assets.AdjunctBatch, GetActiveAdjunctBatches, AdjunctBatch>(
            new GetActiveAdjunctBatches(customerId),
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get active adjunct batches failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of customer recent adjunct batches. These are all batches that are finished, ordered by latest
    /// finished.
    ///
    /// Supports ?page= and ?pageSize= query parameters for paging
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD collection of AdjunctBatch objects</returns>
    [HttpGet]
    [Route("recent")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<AdjunctBatch>))]
    public async Task<IActionResult> GetRecentAdjunctBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        return await HandlePagedFetch<DLCS.Model.Assets.AdjunctBatch, GetRecentAdjunctBatches, AdjunctBatch>(
            new GetRecentAdjunctBatches(customerId),
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get recent adjunct batches failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of specified adjunct batch.
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of adjunct batch to load</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD AdjunctBatch object</returns>
    [HttpGet]
    [Route("batches/{batchId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdjunctBatch))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetAdjunctBatch(
        [FromRoute] int customerId,
        [FromRoute] int batchId,
        CancellationToken cancellationToken)
    {
        return await HandleFetch(
            new GetAdjunctBatch(customerId, batchId),
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get adjunct batch failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of all adjuncts currently associated with specified batch.
    ///
    /// Supports the following query parameters:
    ///   ?orderBy= and ?orderByDescending= for ordering (Created is the only supported field)
    ///   ?page= and ?pageSize= for paging
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of adjunct batch to load adjuncts from</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD collection of Adjunct objects</returns>
    [HttpGet]
    [Route("batches/{batchId:int}/current")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetBatchCurrentAdjuncts(
        [FromRoute] int customerId, [FromRoute] int batchId, CancellationToken cancellationToken)
    {
        return await HandlePagedFetch<DLCS.Model.Assets.Adjunct, GetBatchCurrentAdjuncts, Adjunct>(
            new GetBatchCurrentAdjuncts(customerId, batchId),
            adjunct => adjunct.ToHydra(GetUrlRoots()),
            errorTitle: "Get current batch adjuncts failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of all adjuncts within specified batch. This includes adjuncts that were part of the batch
    /// at creation time, as long as they still exist, even if they have since been reassigned to another batch.
    ///
    /// Supports the following query parameters:
    ///   ?orderBy= and ?orderByDescending= for ordering (Created is the only supported field)
    ///   ?page= and ?pageSize= for paging
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of adjunct batch to load adjuncts from</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD collection of Adjunct objects</returns>
    [HttpGet]
    [Route("batches/{batchId:int}/adjuncts")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<Adjunct>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetBatchAdjuncts(
        [FromRoute] int customerId, [FromRoute] int batchId, CancellationToken cancellationToken)
    {
        return await HandlePagedFetch<DLCS.Model.Assets.Adjunct, GetBatchAdjuncts, Adjunct>(
            new GetBatchAdjuncts(customerId, batchId),
            adjunct => adjunct.ToHydra(GetUrlRoots()),
            errorTitle: "Get batch adjuncts failed",
            cancellationToken: cancellationToken);
    }

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

        // Convert at controller boundary; ToDlcsModel(customerId) throws BadRequestException
        // for invalid/mismatched asset refs, caught by HydraExceptionFilter
        var dlcsAdjuncts = adjuncts.Members!
            .Select(a => a.ToDlcsModel(customerId))
            .ToArray();

        return await HandleUpsert(
            new CreateAdjunctBatch(customerId, dlcsAdjuncts),
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Create adjunct batch failed",
            cancellationToken: cancellationToken);
    }
}
