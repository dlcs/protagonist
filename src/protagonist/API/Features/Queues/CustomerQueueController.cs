using API.Converters;
using API.Features.Queues.Converters;
using API.Features.Queues.Requests;
using API.Features.Queues.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Processing;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Batch = DLCS.HydraModel.Batch;

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
    /// Get details of default customer queue
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
            errorTitle: "Get Customer Queue failed",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// POST /customers/{customerId}/queue
    ///
    /// Create a batch of images to ingest
    /// </summary>
    /// <param name="customerId">Id of customer to create batch for</param>
    /// <param name="images">Hydra collection of assets to add to batch</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Batch object</returns>
    [HttpPost]
    public async Task<IActionResult> CreateBatch(
        [FromRoute] int customerId,
        [FromBody] HydraCollection<DLCS.HydraModel.Image> images,
        [FromServices] QueuePostValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(images, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        var request =
            new CreateBatchOfImages(customerId, images.Members!.Select(i => i.ToDlcsModel(customerId)).ToList());

        return await HandleUpsert(request,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Create batch failed",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// POST /customers/{customerId}/queue/priority
    ///
    /// Create a batch of images to ingest, adding request to priority queue
    /// </summary>
    /// <param name="customerId">Id of customer to create batch for</param>
    /// <param name="images">Hydra collection of assets to add to batch</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Batch object</returns>
    [HttpPost]
    [Route("priority")]
    public async Task<IActionResult> CreatePriorityBatch(
        [FromRoute] int customerId,
        [FromBody] HydraCollection<DLCS.HydraModel.Image> images,
        [FromServices] QueuePostValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(images, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var request =
            new CreateBatchOfImages(customerId, images.Members!.Select(i => i.ToDlcsModel(customerId)).ToList(),
                QueueNames.Priority);

        return await HandleUpsert(request,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Create priority batch failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// GET /customers/{customerId}/queue/priority
    ///
    /// Get details of priority customer queue
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("priority")]
    public async Task<IActionResult> GetCustomerPriorityQueue([FromRoute] int customerId, 
        CancellationToken cancellationToken)
    {
        var getCustomerRequest = new GetCustomerQueue(customerId, "priority");

        return await HandleFetch(
            getCustomerRequest,
            queue => queue.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get Customer Priority Queue failed",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// GET /customers/{customerId}/queue/batches/{batchId}
    /// 
    /// Get details of specified batches 
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
    /// GET /customers/{customerId}/queue/batches/{batchId}/images
    /// 
    /// Get details of all images within batch.
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of batch to load images from</param>
    /// <param name="q">
    /// Optional query parameter. A serialised JSON <see cref="AssetFilter"/> object</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("batches/{batchId}/images")]
    public async Task<IActionResult> GetBatchImages([FromRoute] int customerId, [FromRoute] int batchId,
        [FromQuery] string? q = null, CancellationToken cancellationToken = default)
    {
        var assetFilter = Request.GetAssetFilterFromQParam(q);
        assetFilter = Request.UpdateAssetFilterFromQueryStringParams(assetFilter);
        if (q.HasText() && assetFilter == null)
        {
            return this.HydraProblem("Could not parse query", null, 400);
        }

        var getCustomerRequest = new GetBatchImages(customerId, batchId, assetFilter);

        return await HandlePagedFetch<Asset, GetBatchImages, DLCS.HydraModel.Image>(
            getCustomerRequest,
            image => image.ToHydra(GetUrlRoots()),
            errorTitle: "Get Batch Images failed",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// POST /customers/{customerId}/queue/batches/{batchId}test
    ///
    /// Tests batch to check if superseded or completed and updates accordingly.
    /// 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of batch to test</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// If batch found will always return a 200. Content will be { "success" : true } if batch has been updated (ie
    /// superseded or complete), or { "success": false } if batch found but not modified
    /// </returns>
    [HttpPost]
    [Route("batches/{batchId}/test")]
    public async Task<IActionResult> TestBatch([FromRoute] int customerId, [FromRoute] int batchId,
        CancellationToken cancellationToken = default)
    {
        return await HandleHydraRequest(async () =>
        {
            var testBatch = new TestBatch(customerId, batchId);

            var response = await Mediator.Send(testBatch, cancellationToken);

            return response == null ? this.HydraNotFound() : Ok(new { success = response });
        }, "Test batch failed");
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