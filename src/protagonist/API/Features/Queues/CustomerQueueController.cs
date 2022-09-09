using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Queues.Converters;
using API.Features.Queues.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using Hydra.Collections;
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
    /// GET /customers/{customerId}/queue/active
    ///
    /// Get details of customer active queues. An "active" queue is one that is incomplete.
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("active")]
    public async Task<IActionResult> GetActiveBatches([FromRoute] int customerId, CancellationToken cancellationToken)
    {
        var getActiveBatchesRequest = new GetActiveBatches(customerId);

        return await HandlePagedFetch<DLCS.Model.Assets.Batch, GetActiveBatches, Batch>(
            getActiveBatchesRequest,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get active batches failed",
            cancellationToken: cancellationToken);
    }
}