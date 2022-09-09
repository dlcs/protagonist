using System.Threading;
using System.Threading.Tasks;
using API.Features.Queues.Converters;
using API.Features.Queues.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Queues;

/// <summary>
/// Controller for handling requests for Batch and Queue resources
/// </summary>
[Route("/customers/{customerId}/queue")]
[ApiController]
public class CustomerQueueController : HydraController
{
    /// <inheritdoc />
    public CustomerQueueController(IOptions<ApiSettings> settings, IMediator mediator) : base(settings.Value, mediator)
    {
    }

    /// <summary>
    /// GET /customers/{customerId}/queue
    ///
    /// Get details of customer queue
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    public async Task<IActionResult> GetCustomerQueue([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        var getCustomerRequest = new GetCustomerQueue(customerId);

        return await HandleFetch(
            getCustomerRequest,
            queue => queue.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get CustomerQueue failed",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// GET /customers/{customerId}/queue/batches/{batchId}
    /// 
    /// Get details of all customer batches 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of batch to load</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("batches/{batchId}")]
    public async Task<IActionResult> GetBatch([FromRoute] int customerId, [FromRoute] int batchId, 
        CancellationToken cancellationToken)
    {
        var getCustomerRequest = new GetBatch(customerId, batchId);

        return await HandleFetch(
            getCustomerRequest,
            queue => queue.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get Batch failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// GET /customers/{customerId}/queue/batches
    ///
    /// Get details of all customer batches 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("batches")]
    public async Task<IActionResult> GetBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        var getBatches = new GetBatches(customerId);

        return await HandlePagedFetch<DLCS.Model.Assets.Batch, GetBatches, Batch>(
            getBatches,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get batches failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// GET /customers/{customerId}/queue/active
    ///
    /// Get details of customer active batches. An "active" batch is one that is incomplete and has not been superseded 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("active")]
    public async Task<IActionResult> GetActiveBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        var getActiveBatches = new GetActiveBatches(customerId);

        return await HandlePagedFetch<DLCS.Model.Assets.Batch, GetActiveBatches, Batch>(
            getActiveBatches,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get active batches failed",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// GET /customers/{customerId}/queue/recent
    ///
    /// Get details of customer recent batches. These are all batches that are finished, ordered by latest finished.
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("recent")]
    public async Task<IActionResult> GetRecentBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        var getRecentBatches = new GetRecentBatches(customerId);

        return await HandlePagedFetch<DLCS.Model.Assets.Batch, GetRecentBatches, Batch>(
            getRecentBatches,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get recent batches failed",
            cancellationToken: cancellationToken);
    }
}