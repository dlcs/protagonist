using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Image.Requests;
using API.Features.Space.Requests;
using API.Settings;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
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
/// 
/// </summary>
[Route("/customers/{customerId}/spaces/{spaceId}/images")]
[ApiController]
public class ImageController : HydraController
{
    private readonly IMediator mediator;
    private readonly ILogger<ImageController> logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ImageController(
        IMediator mediator,
        IOptions<ApiSettings> options,
        ILogger<ImageController> logger) : base(options.Value)
    {
        this.mediator = mediator;
        this.logger = logger;
    }

    /// <summary>
    /// GET /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    /// 
    /// A single Hydra Image.
    /// </summary>
    /// <param name="customerId">(from resource path)</param>
    /// <param name="spaceId">(from resource path)</param>
    /// <param name="imageId">(from resource path)</param>
    /// <returns>A Hydra JSON-LD Image object representing the Asset.</returns>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType(404, Type = typeof(Error))]
    [Route("{imageId}")]
    public async Task<IActionResult> GetImage(int customerId, int spaceId, string imageId)
    {
        var assetId = new AssetId(customerId, spaceId, imageId);
        var dbImage = await mediator.Send(new GetImage(assetId));
        if (dbImage == null)
        {
            return HydraNotFound();
        }
        return Ok(dbImage.ToHydra(GetUrlRoots()));
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
    /// <param name="q">A serialised JSON <see cref="ImageQuery"/> object</param>
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
            return HydraProblem("Could not parse query", null, 400);
        }
        var orderByField = GetOrderBy(orderBy, orderByDescending, out var descending);
        var imagesRequest = new GetSpaceImages(descending, page.Value, pageSize.Value, 
            spaceId, customerId, orderByField, assetFilter);
        var spaceImagesResult = await mediator.Send(imagesRequest);
        if (!spaceImagesResult.SpaceExistsForCustomer || spaceImagesResult.PageOfAssets == null)
        {
            return HydraNotFound(spaceImagesResult.Errors?[0]);
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
    /// PUT /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    /// 
    /// PUT an asset to its ID location
    /// </summary>
    /// <param name="customerId">(from resource path)</param>
    /// <param name="spaceId">(from resource path)</param>
    /// <param name="imageId">(from resource path)</param>
    /// <param name="hydraAsset">The body of the request contains the Asset in Hydra JSON-LD form (Image class)</param>
    /// <returns>The created or updated Hydra Image object for the Asset</returns>
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType((int)HttpStatusCode.Created, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.MethodNotAllowed, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InsufficientStorage, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotImplemented, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError, Type = typeof(ProblemDetails))]
    [HttpPut]
    [Route("{imageId}")]
    public async Task<IActionResult> PutImage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] DLCS.HydraModel.Image hydraAsset)
    {
        return await PutOrPatchImage(customerId, spaceId, imageId, hydraAsset);
    }

    /// <summary>
    /// PATCH /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    /// 
    /// PATCH asset at that location.
    /// </summary>
    /// <param name="customerId">(from resource path)</param>
    /// <param name="spaceId">(from resource path)</param>
    /// <param name="imageId">(from resource path)</param>
    /// <param name="hydraAsset">The body of the request contains the Asset in Hydra JSON-LD form (Image class)</param>
    /// <returns>The created or updated Hydra Image object for the Asset</returns>
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DLCS.HydraModel.Image))] // for PATCH
    [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.MethodNotAllowed, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InsufficientStorage, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.NotImplemented, Type = typeof(ProblemDetails))]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError, Type = typeof(ProblemDetails))]
    [HttpPatch]
    [Route("{imageId}")]
    public async Task<IActionResult> PatchImage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] DLCS.HydraModel.Image hydraAsset)
    {
        return await PutOrPatchImage(customerId, spaceId, imageId, hydraAsset);
    }


    /// <summary>
    /// PUT   /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    /// PATCH /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    /// 
    /// PUT an asset to its ID location or PATCH asset at that location.
    /// (Unlike Deliverator, the same method handles a PATCH.)
    /// </summary>
    /// <param name="customerId">(from resource path)</param>
    /// <param name="spaceId">(from resource path)</param>
    /// <param name="imageId">(from resource path)</param>
    /// <param name="hydraAsset">The body of the request contains the Asset in Hydra JSON-LD form (Image class)</param>
    /// <returns>The created or updated Hydra Image object for the Asset</returns>
    private async Task<IActionResult> PutOrPatchImage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] DLCS.HydraModel.Image hydraAsset)
    {
        // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L74
            
        var assetId = new AssetId(customerId, spaceId, imageId);
        var asset = hydraAsset.ToDlcsModel(customerId, spaceId, imageId);
        asset.Id = assetId.ToString();
    
        // In the special case where we were passed ImageWithFile from the PostImageWithFileBytes action, 
        // it was a POST - but we should revisit that as the direct image ingest should be a PUT as well I think
        // See https://github.com/dlcs/protagonist/issues/338
        var method = hydraAsset is ImageWithFile ? "PUT" : Request.Method;
        
        var request = new PutOrPatchImage { Asset = asset, Method = method };
        var result = await mediator.Send(request);
        if (result.Asset != null && result.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            var hydraResponse = result.Asset.ToHydra(GetUrlRoots());
            if (hydraResponse.Id.HasText())
            {
                switch (result.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return Ok(hydraResponse);
                    case HttpStatusCode.Created:
                        return Created(hydraResponse.Id, hydraResponse);
                }
            }
            else
            {
                return Problem("No ID in returned Image", asset.Id, 
                    (int?)result.StatusCode, "PUT or PATCH of Asset failed");
            }
        }
            
        return Problem(result.Message, asset.Id, 
            (int?)result.StatusCode, "PUT or PATCH of Asset failed");
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
                return HydraProblem(
                    "Bulk patching operations may not contain origin or image policy information.", 
                    null, 400, "Not Supported");
            }
            
            if (images.Members.Any(image => image.ModelId == null))
            {
                return HydraProblem(
                    "All assets must have a ModelId", 
                    null, 400, "Missing identifier");
            }
            foreach (var hydraImage in images.Members)
            {
                try
                {
                    var asset = hydraImage.ToDlcsModel(customerId, spaceId);
                    var request = new PutOrPatchImage { Asset = asset, Method = "PATCH" };
                    var result = await mediator.Send(request);
                    if (result.Asset != null)
                    {
                        patchedAssets.Add(result.Asset);
                    }
                    else
                    {
                        logger.LogError("We did not get an asset back for {AssetId}", asset.Id);
                    }
                }
                catch (APIException apiEx)
                {
                    return HydraProblem(
                        apiEx.Message, 
                        null, 500, apiEx.Label);
                }
                catch (Exception ex)
                {
                    return HydraProblem(
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


    // ############################# POST BYTES OF IMAGE #############################
    /// <summary>
    /// POST /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    /// 
    /// Ingest specified file bytes to DLCS.
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
    [Route("{imageId}")]
    public async Task<IActionResult> PostImageWithFileBytes([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, [FromBody] ImageWithFile asset)
    {
        const string errorTitle = "POST of Asset bytes failed";
        var assetId = new AssetId(customerId, spaceId, imageId);
        if (asset.File == null || asset.File.Length == 0)
        {
            return Problem("No file bytes in request body", assetId.ToString(),
                (int?)HttpStatusCode.BadRequest, errorTitle);
        }
        if (asset.MediaType.IsNullOrEmpty())
        {
            return Problem("MediaType must be supplied", assetId.ToString(),
                (int?)HttpStatusCode.BadRequest, errorTitle);
        }
        var saveRequest = new HostAssetAtOrigin
        {
            AssetId = assetId,
            FileBytes = asset.File,
            MediaType = asset.MediaType
        };

        var result = await mediator.Send(saveRequest);
        if (string.IsNullOrEmpty(result.Origin))
        {
            return Problem("Could not save uploaded file", assetId.ToString(), 500, errorTitle);
        }

        asset.Origin = result.Origin;
        asset.File = null;

        return await PutOrPatchImage(customerId, spaceId, imageId, asset);

    }
}