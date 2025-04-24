using API.Converters;
using API.Features.Customer.Requests;
using API.Features.Customer.Validation;
using API.Features.Image.Validation;
using API.Infrastructure;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Web.Requests;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Customer;

/// <summary>
/// Controller for handling bulk requests for images associated with a customer 
/// </summary>
[Route("/customers/{customerId}")]
[ApiController]
public class CustomerImagesController : HydraController
{
    public CustomerImagesController(IOptions<ApiSettings> settings, IMediator mediator) : base(settings.Value, mediator)
    {
    }
    
    /// <summary>
    /// Accepts a list of image identifiers, will return a list of matching images.
    ///
    /// This endpoint doesn't support paging - all results are returned in single page 
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST: /customers/1/allImages
    ///     {
    ///         "@context": "http://www.w3.org/ns/hydra/context.jsonld",
    ///         "@type":"Collection",
    ///         "member": [
    ///             { "id": "1/1/foo" },
    ///             { "id": "1/99/bar" }
    ///         ]
    ///     }
    /// </remarks>
    [HttpPost]
    [Route("allImages")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    public async Task<IActionResult> GetAllImages(
        [FromRoute] int customerId,
        [FromBody] HydraCollection<IdentifierOnly> imageIdentifiers,
        [FromServices] ImageIdListValidator validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(imageIdentifiers.Members, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var request = new GetMultipleImagesById(imageIdentifiers.Members!.Select(m => m.Id).ToList(), customerId);

        return await HandleListFetch<Asset, GetMultipleImagesById, DLCS.HydraModel.Image>(
            request,
            a => a.ToHydra(GetUrlRoots()),
            "Get customer images failed",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Accepts a list of image identifiers in a query string, will return a list of matching images.
    ///
    /// This endpoint supports paging 
    /// </summary>
    [HttpGet]
    [Route("allImages")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    public async Task<IActionResult> GetAllImagesPaged(
        [FromRoute] int customerId,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var assetFilter = Request.GetAssetFilterFromQParam(q);
        assetFilter = Request.UpdateAssetFilterFromQueryStringParams(assetFilter);
        if (q.HasText() && assetFilter == null)
        {
            return this.HydraProblem("Could not parse query", null, 400);
        }

        var getQueriedAllImages = new GetQueriedAllImages(customerId, assetFilter);

        return await HandlePagedFetch<Asset, GetQueriedAllImages, DLCS.HydraModel.Image>(
            getQueriedAllImages,
            image => image.ToHydra(GetUrlRoots()),
            errorTitle: "Get All Images failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Accepts a list of image identifiers, will update a list of matching images.
    ///
    /// This endpoint doesn't support paging - all results are returned in single page 
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     PATCH: /customers/1/allImages
    ///     {
    ///         "@context": "http://www.w3.org/ns/hydra/context.jsonld",
    ///         "@type":"Collection",
    ///         "member": [
    ///             { "id": "1/1/foo" },
    ///             { "id": "1/99/bar" }
    ///         ],
    ///         "field": "manifests",
    ///         "operation": "add"
    ///         "value": "["first"]"
    ///     }
    /// </remarks>
    [HttpPatch]
    [Route("allImages")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    public async Task<IActionResult> UpdateAllImages(
        [FromRoute] int customerId,
        [FromBody] BulkPatch<IdentifierOnly> bulkPatch,
        [FromServices] BulkAssetPatchValidator validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(bulkPatch, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        return await HandleUpsert(new UpdateAllImages(bulkPatch.Members!.Select(m => m.Id).ToList(), 
                customerId, bulkPatch.Value, bulkPatch.Field, bulkPatch.Operation), al =>
        {
            return new HydraCollection<DLCS.HydraModel.Image>
            {
                WithContext = true,
                Members = al.Select(a => a.ToHydra(GetUrlRoots())).ToArray(),
                TotalItems = al.Count,
                PageSize = al.Count,
                Id = Request.GetJsonLdId()
            };
        }, errorTitle: "Failed to patch all images",
        cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Accepts a list of image identifiers, will delete those that exist from DB
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST: /customers/1/deleteImages
    ///     {
    ///         "@context": "http://www.w3.org/ns/hydra/context.jsonld",
    ///         "@type":"Collection",
    ///         "member": [
    ///             { "id": "1/1/foo" },
    ///             { "id": "1/99/bar" }
    ///         ]
    ///     }
    /// </remarks>
    [HttpPost]
    [Route("deleteImages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteImages(
        [FromRoute] int customerId,
        [FromQuery] string? deleteFrom,
        [FromBody] HydraCollection<IdentifierOnly> imageIdentifiers,
        [FromServices] ImageIdListValidator validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(imageIdentifiers.Members, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var additionalDeletion = ImageCacheTypeConverter.ConvertToImageCacheType(deleteFrom, ',');

        return await HandleHydraRequest(async () =>
        {
            var request =
                new DeleteMultipleImagesById(imageIdentifiers.Members!.Select(m => m.Id).ToList(),
                    customerId, additionalDeletion);
            var deletedRows = await Mediator.Send(request, cancellationToken);

            if (deletedRows == 0)
            {
                return this.HydraProblem("No assets found", null, 400, "Delete images failed");
            }

            // TODO - return a better message (or 204?). This is for backwards compat with Deliverator and
            return Ok(new { message = "images deleted" });
        }, "Delete images failed");
    }
}
