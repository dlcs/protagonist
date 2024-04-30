using System.Net;
using API.Converters;
using API.Features.DeliveryChannels.Converters;
using API.Features.Image.Requests;
using API.Features.Image.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.HydraModel;
using Hydra.Model;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Image;

/// <summary>
/// Controller for handling requests for image (aka Asset) resources
/// </summary>
[Route("/customers/{customerId}/spaces/{spaceId}/images/{imageId}")]
[ApiController]
public class ImageController : HydraController
{
    private readonly ILogger<ImagesController> logger;
    private readonly OldHydraDeliveryChannelsConverter oldHydraDcConverter;
    
    public ImageController(
        IMediator mediator,
        IOptions<ApiSettings> options, 
        ILogger<ImagesController> logger,
        OldHydraDeliveryChannelsConverter oldHydraDcConverter) : base(options.Value, mediator)
    {
        this.logger = logger;
        this.oldHydraDcConverter = oldHydraDcConverter;
    }

    /// <summary>
    /// Get details of a single Hydra Image.
    /// </summary>
    /// <returns>A Hydra JSON-LD Image object representing the Asset.</returns>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> GetImage(int customerId, int spaceId, string imageId)
    {
        var assetId = new AssetId(customerId, spaceId, imageId);
        var dbImage = await Mediator.Send(new GetImage(assetId));
        if (dbImage == null)
        {
            return this.HydraNotFound();
        }
        return Ok(dbImage.ToHydra(GetUrlRoots(), Settings.EmulateOldDeliveryChannelProperties));
    }

    /// <summary>
    /// Create or update asset at specified ID.
    ///
    /// PUT requests always trigger reingesting of asset - in general batch processing should be preferred.
    ///
    /// Image + File assets are ingested synchronously. Timebased assets are ingested asynchronously.
    ///
    /// "File" property should be base64 encoded image, if included.
    /// </summary>
    /// <param name="hydraAsset">The body of the request contains the Asset in Hydra JSON-LD form (Image class)</param>
    /// <returns>The created or updated Hydra Image object for the Asset</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT: /customers/1/spaces/1/images/my-image
    ///     {
    ///         "@type":"Image",
    ///         "family": "I",
    ///         "origin": "https://example.text/.../image.jpeg",
    ///         "mediaType": "image/jpeg",
    ///         "string1": "my-metadata"
    ///     }
    /// </remarks>
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType((int)HttpStatusCode.Created, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.MethodNotAllowed, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InsufficientStorage, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotImplemented, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError, Type = typeof(ProblemDetails))]
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000, ValueLengthLimit = 100_000_000)]
    [HttpPut]
    public async Task<IActionResult> PutImage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] ImageWithFile hydraAsset,
        [FromServices] HydraImageValidator validator,
        CancellationToken cancellationToken)
    {
        if (Settings.LegacyModeEnabledForSpace(customerId, spaceId))
        {
            hydraAsset = LegacyModeConverter.VerifyAndConvertToModernFormat(hydraAsset);
        }
        
        if (Settings.EmulateOldDeliveryChannelProperties && 
            hydraAsset.WcDeliveryChannels != null)
        {
            hydraAsset.DeliveryChannels = oldHydraDcConverter.Convert(hydraAsset);
        }
        
        if(!Settings.EmulateOldDeliveryChannelProperties &&
           (hydraAsset.ImageOptimisationPolicy != null || hydraAsset.ThumbnailPolicy != null))
        {
            return this.HydraProblem("ImageOptimisationPolicy and ThumbnailPolicy are disabled", null,
                400, "Bad Request");
        }
        
        if (hydraAsset.ModelId == null)
        {
            hydraAsset.ModelId = imageId;
        }
        
        var validationResult = await validator.ValidateAsync(hydraAsset, 
            strategy => strategy.IncludeRuleSets("default", "create"), cancellationToken);
        
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        if (!hydraAsset.File.IsNullOrEmpty())
        {
            return await PutOrPatchAssetWithFileBytes(customerId, spaceId, imageId, hydraAsset, cancellationToken);
        }
        
        return await PutOrPatchAsset(customerId, spaceId, imageId, hydraAsset, cancellationToken);
    }

    /// <summary>
    /// Make a partial update to an existing asset resource.
    ///
    /// This may trigger a reingest depending on which fields have been updated.
    /// 
    /// PATCH asset at that location.
    /// </summary>
    /// <param name="hydraAsset">The body of the request contains the Asset in Hydra JSON-LD form (Image class)</param>
    /// <returns>The updated Hydra Image object for the Asset</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     PATCH: /customers/1/spaces/1/images/my-image
    ///     {
    ///         "origin": "https://example.text/.../image.jpeg",
    ///         "string1": "my-new-metadata"
    ///     }
    /// </remarks>
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.MethodNotAllowed, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InsufficientStorage, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotImplemented, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError, Type = typeof(ProblemDetails))]
    [HttpPatch]
    public async Task<IActionResult> PatchImage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] DLCS.HydraModel.Image hydraAsset,
        [FromServices] HydraImageValidator validator,
        CancellationToken cancellationToken)
    {
        if (!hydraAsset.WcDeliveryChannels.IsNullOrEmpty())
        {
            if (!Settings.DeliveryChannelsEnabled)
            {
                var assetId = new AssetId(customerId, spaceId, imageId);
                return this.HydraProblem("Delivery channels are disabled", assetId.ToString(), 400, "Bad Request");
            }
            
            if (Settings.EmulateOldDeliveryChannelProperties)
            {
                hydraAsset.DeliveryChannels = oldHydraDcConverter.Convert(hydraAsset);
            }
        }
        
        if(!Settings.EmulateOldDeliveryChannelProperties &&
           (hydraAsset.ImageOptimisationPolicy != null || hydraAsset.ThumbnailPolicy != null))
        {
            return this.HydraProblem("ImageOptimisationPolicy and ThumbnailPolicy are disabled", null,
                400, "Bad Request");
        }
        
        var validationResult = await validator.ValidateAsync(hydraAsset, 
            strategy => strategy.IncludeRuleSets("default", "patch"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        return await PutOrPatchAsset(customerId, spaceId, imageId, hydraAsset, cancellationToken);
    }

    /// <summary>
    /// DELETE asset at specified location. This will remove asset immediately, generated derivatives will be picked up
    /// and processed eventually. 
    /// </summary>
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [HttpDelete]
    public async Task<IActionResult> DeleteAsset([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, [FromQuery] string? deleteFrom, CancellationToken cancellationToken)
    {
        var additionalDeletion = ImageCacheTypeConverter.ConvertToImageCacheType(deleteFrom, ',');
        
        var deleteRequest = new DeleteAsset(customerId, spaceId, imageId, additionalDeletion);
        var result = await Mediator.Send(deleteRequest, cancellationToken);

        return result switch
        {
            DeleteResult.NotFound => this.HydraNotFound(),
            DeleteResult.Error => this.HydraProblem("Error deleting asset - delete failed", null, 500,
                "Delete Asset failed"),
            _ => NoContent()
        };
    }

    /// <summary>
    /// Reingest asset at specified location
    /// </summary>
    /// <returns>The reingested Hydra Image object for the Asset</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /customers/99/spaces/10/images/changed_image/reingest
    /// </remarks>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [HttpPost]
    [Route("reingest")]
    public Task<IActionResult> ReingestAsset([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, CancellationToken cancellationToken)
    {
        var reingestRequest = new ReingestAsset(customerId, spaceId, imageId);
        return HandleUpsert(reingestRequest, 
            asset => asset.ToHydra(GetUrlRoots(), Settings.EmulateOldDeliveryChannelProperties), 
            reingestRequest.AssetId.ToString(),
            "Reingest Failed", cancellationToken);
    }

    /// <summary>
    /// Ingest specified file bytes to DLCS. Only "I" family assets are accepted.
    /// "File" property should be base64 encoded image. 
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/spaces/1/images/my-image
    ///     {
    ///         "@type":"Image",
    ///         "family": "I",
    ///         "file": "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAM...."
    ///     }
    /// </remarks>
    [ProducesResponseType(201, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType(400, Type = typeof(ProblemDetails))]
    [HttpPost]  // This should be a PUT? But then it will be the same op to same location as a normal asset without File.
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000, ValueLengthLimit = 100_000_000)]
    public async Task<IActionResult> PostImageWithFileBytes(
        [FromRoute] int customerId, 
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] ImageWithFile hydraAsset,
        [FromServices] HydraImageValidator validator,
        CancellationToken cancellationToken)
    {

        logger.LogWarning(
            "Warning: POST /customers/{CustomerId}/spaces/{SpaceId}/images/{ImageId} was called. This route is deprecated.",
            customerId, spaceId, imageId);
        
        return await PutImage(customerId, spaceId, imageId, hydraAsset, validator, cancellationToken);
    }

    /// <summary>
    /// Get transcode metadata for Timebased assets
    /// </summary>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet]
    [Route("metadata")]
    public async Task<IActionResult> GetAssetMetadata([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, CancellationToken cancellationToken)
    {
        return await HandleHydraRequest(async () =>
        {
            var getMetadata = new GetAssetMetadata(customerId, spaceId, imageId);
            var entityResult = await Mediator.Send(getMetadata, cancellationToken);

            return this.FetchResultToHttpResult(entityResult, getMetadata.AssetId.ToString(), "Error getting metadata");
        });
    }

    private Task<IActionResult> PutOrPatchAsset(int customerId, int spaceId, string imageId,
        DLCS.HydraModel.Image hydraAsset, CancellationToken cancellationToken)
    {
        var assetId = new AssetId(customerId, spaceId, imageId);
        var asset = hydraAsset.ToDlcsModel(customerId, spaceId, imageId);
        asset.Id = assetId;
                
        // In the special case where we were passed ImageWithFile from the PostImageWithFileBytes action, 
        // it was a POST - but we should revisit that as the direct image ingest should be a PUT as well I think
        // See https://github.com/dlcs/protagonist/issues/338
        var method = hydraAsset is ImageWithFile ? "PUT" : Request.Method;

        var deliveryChannelsBeforeProcessing = (hydraAsset.DeliveryChannels ?? Array.Empty<DeliveryChannel>())
            .Select(d => new DeliveryChannelsBeforeProcessing(d.Channel, d.Policy)).ToArray();

        var assetBeforeProcessing = new AssetBeforeProcessing(asset, deliveryChannelsBeforeProcessing);

        var createOrUpdateRequest = new CreateOrUpdateImage(assetBeforeProcessing, method);

        return HandleUpsert(
            createOrUpdateRequest,
            asset => asset.ToHydra(GetUrlRoots(), Settings.EmulateOldDeliveryChannelProperties),
            assetId.ToString(),
            "Upsert asset failed", cancellationToken);
    }
    
    private async Task<IActionResult> PutOrPatchAssetWithFileBytes(int customerId, int spaceId, string imageId,
        ImageWithFile hydraAsset, CancellationToken cancellationToken)
    { 
        const string errorTitle = "POST of Asset bytes failed";
        var assetId = new AssetId(customerId, spaceId, imageId);
        if (hydraAsset.File!.Length == 0)
        {
            return this.HydraProblem("No file bytes in request body", assetId.ToString(),
                (int?)HttpStatusCode.BadRequest, errorTitle);
        }
        
        var saveRequest = new HostAssetAtOrigin(assetId, hydraAsset.File!, hydraAsset.MediaType!);

        var result = await Mediator.Send(saveRequest, cancellationToken);
        if (string.IsNullOrEmpty(result.Origin))
        {
            return this.HydraProblem("Could not save uploaded file", assetId.ToString(), 500, errorTitle);
        }

        hydraAsset.Origin = result.Origin;
        hydraAsset.File = null;

        return await PutOrPatchAsset(customerId, spaceId, imageId, hydraAsset, cancellationToken);
    }
}