using API.Features.Queues.Converters;
using API.Features.Queues.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Queues;

/// <summary>
/// Controller for handling requests for overall Queue resources
/// </summary>
[Route("/queue")]
[ApiController]
public class QueueController : HydraController
{
    public QueueController(IOptions<ApiSettings> settings, IMediator mediator) : base(settings.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get counts of requests per queue.
    ///
    /// This includes DLCS-internal queue's and external queues (e.g. SQS) 
    /// </summary>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(QueueSummary))]
    public async Task<IActionResult> GetQueueDetails(CancellationToken cancellationToken)
    {
        var getCustomerRequest = new GetQueueCounts();

        return await HandleFetch(
            getCustomerRequest,
            queue => queue.ToHydra(GetUrlRoots().BaseUrl),
            cancellationToken: cancellationToken
        );
    }
}