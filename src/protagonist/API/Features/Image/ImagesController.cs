using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Exceptions;
using API.Features.Image.Requests;
using API.Features.Space.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Web.Requests;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Image;

/// <summary>
/// Controller for handling requests for collections of image (aka Asset) resources
/// </summary>
[Route("/customers/{customerId}/spaces/{spaceId}/images")]
[ApiController]
public class ImagesController : HydraController
{
    private readonly ILogger<ImagesController> logger;

    /// <inheritdoc />
    public ImagesController(
        IMediator mediator,
        IOptions<ApiSettings> options,
        ILogger<ImagesController> logger) : base(options.Value, mediator)
    {
        this.logger = logger;
    }
    
    /// <summary>
    /// GET /customers/{customerId}/spaces/{spaceId}/images
    /// 
    /// A page of images within a Space.
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="spaceId"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="orderBy"></param>
    /// <param name="orderByDescending"></param>
    /// <param name="q">A serialised JSON <see cref="AssetFilter"/> object</param>
    /// <returns>A Hydra Collection of Image objects as JSON-LD</returns>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> GetImages(
        int customerId, int spaceId,
        int? page = 1, int? pageSize = -1,
        string? orderBy = null, string? orderByDescending = null,
        string? q = null)
    {
        if (pageSize is null or < 0) pageSize = Settings.PageSize;
        if (page is null or < 0) page = 1;
        var assetFilter = Request.GetAssetFilterFromQParam(q);
        assetFilter = Request.UpdateAssetFilterFromQueryStringParams(assetFilter);
        if (q.HasText() && assetFilter == null)
        {
            return this.HydraProblem("Could not parse query", null, 400);
        }
        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);
        var imagesRequest = new GetSpaceImages(descending, page.Value, pageSize.Value, 
            spaceId, customerId, orderByField, assetFilter);
        var spaceImagesResult = await mediator.Send(imagesRequest);
        if (!spaceImagesResult.SpaceExistsForCustomer || spaceImagesResult.PageOfAssets == null)
        {
            return this.HydraNotFound(spaceImagesResult.Errors?[0]);
        }

        var urlRoots = GetUrlRoots();
        var collection = new HydraCollection<DLCS.HydraModel.Image>
        {
            WithContext = true,
            Members = spaceImagesResult.PageOfAssets.Assets
                .Select(a => a.ToHydra(urlRoots)).ToArray(),
            TotalItems = spaceImagesResult.PageOfAssets.Total,
            PageSize = pageSize,
            Id = Request.GetJsonLdId()
        };
        PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value, orderByField, descending);
        return Ok(collection);
    }

    /// <summary>
    /// PATCH /customers/{customerId}/spaces/{spaceId}/images
    /// 
    /// PATCH a collection of images.
    /// This is for bulk patch operations on images in the same space.
    /// </summary>
    /// <param name="customerId">(from resource path)</param>
    /// <param name="spaceId">(from resource path)</param>
    /// <param name="images">The JSON-LD request body, a HydraCollection of Hydra Image objects.</param>
    /// <returns>A HydraCollection of the updated Assets, as Hydra Image objects.</returns>
    [HttpPatch]
    [ProducesResponseType(200, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(400, Type = typeof(Error))]
    public async Task<IActionResult> PatchImages(
        [FromRoute] int customerId, [FromRoute] int spaceId,
        [FromBody] HydraCollection<DLCS.HydraModel.Image> images)
    {
        // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImages.cs#L147
            
        var patchedAssets = new List<Asset>();
            
        // Should there be a size limit on how many assets can be patched in a single go?
        // https://github.com/dlcs/protagonist/issues/339

        if (images.Members is { Length: > 0 })
        {
            if (BulkPatchMayCauseReprocessing(images))
            {
                return this.HydraProblem(
                    "Bulk patching operations may not contain origin or image policy information.", 
                    null, 400, "Not Supported");
            }
            
            if (images.Members.Any(image => image.ModelId == null))
            {
                return this.HydraProblem(
                    "All assets must have a ModelId", 
                    null, 400, "Missing identifier");
            }
            foreach (var hydraImage in images.Members)
            {
                try
                {
                    var asset = hydraImage.ToDlcsModel(customerId, spaceId);
                    var request = new CreateOrUpdateImage(asset, "PATCH");
                    var result = await mediator.Send(request);
                    if (result.Entity != null)
                    {
                        patchedAssets.Add(result.Entity);
                    }
                    else
                    {
                        logger.LogError("We did not get an asset back for {AssetId}", asset.Id);
                    }
                }
                catch (APIException apiEx)
                {
                    return this.HydraProblem(
                        apiEx.Message, 
                        null, 500, apiEx.Label);
                }
                catch (Exception ex)
                {
                    return this.HydraProblem(
                        ex.Message, 
                        null, 500, "Could not patch images");
                }
            }
        }

        var urlRoots = GetUrlRoots();
        var output = new HydraCollection<DLCS.HydraModel.Image>
        {
            WithContext = true,
            Members = patchedAssets.Select(a => a.ToHydra(urlRoots)).ToArray(),
            TotalItems = patchedAssets.Count,
            Id = Request.GetDisplayUrl() + "?patch_" + Guid.NewGuid()
        };
        return Ok(output);
    }

    private bool BulkPatchMayCauseReprocessing(HydraCollection<DLCS.HydraModel.Image> images)
    {
        if (images.Members == null) return false;
        
        // This should check the same things as AssetPreparer::PrepareAssetForUpsert
        // But we don't want to call that at the controller level; we haven't acquired 
        // any existing assets yet.
        return images.Members.Any(image => 
            image.Origin.HasText() || 
            image.ImageOptimisationPolicy.HasText()
            );
    }
}