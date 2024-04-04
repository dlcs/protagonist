using System.Collections.Generic;
using API.Converters;
using API.Features.DeliveryChannels.Converters;
using API.Features.Image;
using API.Features.Queues.Converters;
using API.Features.Queues.Requests;
using API.Features.Queues.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Model.Processing;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Http;
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
    private readonly ApiSettings apiSettings;
    private readonly OldHydraDeliveryChannelsConverter oldHydraDcConverter;   
    public CustomerQueueController(IOptions<ApiSettings> settings, IMediator mediator, 
        OldHydraDeliveryChannelsConverter oldHydraDcConverter) : base(settings.Value, mediator)
    {
        apiSettings = settings.Value;
        this.oldHydraDcConverter = oldHydraDcConverter;
    }

    /// <summary>
    /// Get details of default customer queue
    /// </summary>
    /// <param name="customerId">Id of customer to get queue details for</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    /// Create a batch of images to ingest.
    ///
    /// These will be enqueued for processing and asynchronously ingested
    /// </summary>
    /// <param name="customerId">Id of customer to create batch for</param>
    /// <param name="images">Hydra collection of assets to add to batch</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Batch object</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/99/queue
    ///     {
    ///         "@context": "http://www.w3.org/ns/hydra/context.jsonld",
    ///         "@type": "Collection",
    ///         "member": [
    ///         {
    ///             "id": "foo",
    ///             "space": 1,
    ///             "origin": "https://example.origin/foo.jpg",
    ///             "family": "I",
    ///             "mediaType": "image/jpeg"
    ///         },
    ///         {
    ///             "id": "bar",
    ///             "space": 2,
    ///             "origin": "https://example.origin/movie.mp4",
    ///             "family": "T",
    ///             "mediaType": "video/mp4"
    ///         }
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBatch(
        [FromRoute] int customerId,
        [FromBody] HydraCollection<DLCS.HydraModel.Image> images,
        [FromServices] QueuePostValidator validator,
        [FromServices] OldHydraDeliveryChannelsConverter oldHydraDcConverter,
        CancellationToken cancellationToken)
    {
        UpdateMembers(customerId, images.Members);

        if (apiSettings.EmulateOldDeliveryChannelProperties)
        {
            ConvertOldDeliveryChannelsForMembers(images.Members);
        }

        var validationResult = await validator.ValidateAsync(images, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
    
        var assetsBeforeProcessing = CreateAssetsBeforeProcessing(customerId, images);

        var request =
            new CreateBatchOfImages(customerId, assetsBeforeProcessing);

        return await HandleUpsert(request,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Create batch failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates assets for legacy mode compatibility and mints GUIDs if no ID set
    /// </summary>
    /// <param name="customerId">The customer id</param>
    /// <param name="members">The assets to update</param>
    private void UpdateMembers(int customerId, IList<DLCS.HydraModel.Image>? members)
    {
        if (members != null)
        {
            if (apiSettings.LegacyModeEnabledForCustomer(customerId))
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (apiSettings.LegacyModeEnabledForSpace(customerId, members[i].Space))
                    {
                        members[i] = LegacyModeConverter.VerifyAndConvertToModernFormat(members[i]);
                    }
                }
            }
            
            foreach (var image in members.Where(image =>  string.IsNullOrEmpty(image.ModelId)))
            {
                image.ModelId = Guid.NewGuid().ToString();
            }
        }
    }

    /// <summary>
    /// Converts WcDeliveryChannels (if set) to DeliveryChannels for a list of assets
    /// </summary>
    /// <param name="members">The assets to update</param>
    private void ConvertOldDeliveryChannelsForMembers(IList<DLCS.HydraModel.Image>? members)
    {
        if (members == null) return;
        
        foreach (var hydraAsset in members)
        {
            if (hydraAsset.WcDeliveryChannels.IsNullOrEmpty()) continue;
            hydraAsset.DeliveryChannels = oldHydraDcConverter.Convert(hydraAsset);
        }
    }

    /// <summary>
    /// Create a batch of images to ingest, adding request to priority queue
    ///
    /// The processing is the same but the priority queue is for images that need to be processed quickly.
    /// Only Image assets are supported
    /// </summary>
    /// <param name="customerId">Id of customer to create batch for</param>
    /// <param name="images">Hydra collection of assets to add to batch</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Batch object</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/99/queue/priority
    ///     {
    ///         "@context": "http://www.w3.org/ns/hydra/context.jsonld",
    ///         "@type": "Collection",
    ///         "member": [
    ///         {
    ///             "id": "foo",
    ///             "space": 1,
    ///             "origin": "https://example.origin/foo.jpg",
    ///             "family": "I",
    ///             "mediaType": "image/jpeg"
    ///         }
    ///     }
    /// </remarks>
    [HttpPost]
    [Route("priority")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePriorityBatch(
        [FromRoute] int customerId,
        [FromBody] HydraCollection<DLCS.HydraModel.Image> images,
        [FromServices] QueuePostValidator validator,
        CancellationToken cancellationToken)
    {
        UpdateMembers(customerId, images.Members);
        
        var validationResult = await validator.ValidateAsync(images, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        var assetsBeforeProcessing = CreateAssetsBeforeProcessing(customerId, images);

        var request =
            new CreateBatchOfImages(customerId, assetsBeforeProcessing, QueueNames.Priority);

        return await HandleUpsert(request,
            batch => batch.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Create priority batch failed",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of priority customer queue
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("priority")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    /// Get details of specified batch 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of batch to load</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("batches/{batchId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    /// Get details of all images within specified batch.
    ///
    /// Supports the following query parameters:
    ///   ?q= parameter for filtering
    ///   ?orderBy= and ?orderByDescending= for ordering
    ///   ?page= and ?pageSize= for paging 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of batch to load images from</param>
    /// <param name="q">
    /// Optional query parameter. A serialised JSON <see cref="AssetFilter"/> object</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET: /customers/1/queue/12345/images?q={"string1":"metadata-value"}
    ///     GET: /customers/1/queue/12345/images?orderByDescending=width
    ///     GET: /customers/1/queue/12345/images?orderBy=height
    ///     GET: /customers/1/queue/12345/images?orderBy=width&page=2&pageSize=10
    /// </remarks>
    [HttpGet]
    [Route("batches/{batchId}/images")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    /// Tests batch to check if superseded or completed and updates underlying batch accordingly.
    ///
    /// Post empty body.
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="batchId">Id of batch to test</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>
    /// If batch found will always return a 200. Content will be { "success" : true } if batch has been updated (ie
    /// superseded or complete), or { "success": false } if batch found but not modified
    /// </returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/queue/batches/12345/test
    ///     { }
    /// </remarks>
    [HttpPost]
    [Route("batches/{batchId}/test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestBatch([FromRoute] int customerId, [FromRoute] int batchId,
        CancellationToken cancellationToken = default)
    {
        return await HandleHydraRequest(async () =>
        {
            var testBatch = new TestBatch(customerId, batchId);

            var response = await Mediator.Send(testBatch, cancellationToken);

            // TODO - return a better message. This is for backwards compat
            return response == null ? this.HydraNotFound() : Ok(new { success = response });
        }, "Test batch failed");
    }

    /// <summary>
    /// Get details of all customer batches.
    ///
    /// Supports ?page= and ?pageSize= query parameters for paging 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>Hydra JSON-LD Queue object</returns>
    [HttpGet]
    [Route("batches")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    /// Get details of customer active batches. An "active" batch is one that is incomplete and has not been superseded.
    ///
    /// Supports ?page= and ?pageSize= query parameters for paging  
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
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
    /// Get details of customer recent batches. These are all batches that are finished, ordered by latest finished.
    ///
    /// Supports ?page= and ?pageSize= query parameters for paging 
    /// </summary>
    /// <param name="customerId">Id of customer</param>
    /// <param name="cancellationToken">Current cancellation token</param>
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
    
    private static List<AssetBeforeProcessing> CreateAssetsBeforeProcessing(int customerId, HydraCollection<DLCS.HydraModel.Image> images)
    {
        var assetsBeforeProcessing = images.Members!
            .Select(i => new AssetBeforeProcessing(i.ToDlcsModel(customerId),
                (i.DeliveryChannels ?? Array.Empty<DeliveryChannel>())
                .Select(d => new DeliveryChannelsBeforeProcessing(d.Channel, d.Policy)).ToArray())).ToList();
        return assetsBeforeProcessing;
    }
}