using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Image.Requests;
using API.Features.Space.Requests;
using API.Settings;
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

[Route("/customers/{customerId}/spaces/{spaceId}/images")]
[ApiController]
public class ImageController : HydraController
{
    private readonly IMediator mediator;
    private readonly ApiSettings settings;
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
        ILogger<ImageController> logger)
    {
        this.mediator = mediator;
        settings = options.Value;
        this.logger = logger;
    }

        
        
    /// <summary>
    /// Ingest specified file bytes to DLCS.
    /// "File" property should be base64 encoded image. 
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT: /customers/1/spaces/1/images/my-image
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
    public async Task<IActionResult> IngestBytes([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, [FromBody] ImageWithFile asset)
    {
        const string errorTitle = "POST of Asset bytes failed";
        var assetId = new AssetId(customerId, spaceId, imageId);
        if (asset.File == null || asset.File.Length == 0)
        {
            return Problem("No file bytes in request body", assetId.ToString(),
                (int?)HttpStatusCode.BadRequest, errorTitle);
        }
        var saveRequest = new HostAssetAtOrigin
        {
            AssetId = assetId,
            FileBytes = asset.File
        };

        var result = await mediator.Send(saveRequest);
        if (string.IsNullOrEmpty(result.Origin))
        {
            return Problem("Could not save uploaded file", assetId.ToString(), 500, errorTitle);
        }

        asset.Origin = result.Origin;
        asset.File = null;
        return await Image(customerId, spaceId, imageId, asset);

    }
        
        
        
    /// <summary>
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="spaceId"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="orderBy"></param>
    /// <param name="orderByDescending"></param>
    /// <returns></returns>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(404, Type = typeof(Error))]
    public async Task<IActionResult> Images(
        int customerId, int spaceId,
        int? page = 1, int? pageSize = -1,
        string? orderBy = null, string? orderByDescending = null)
    {
        if (pageSize is null or < 0) pageSize = settings.PageSize;
        if (page is null or < 0) page = 1;
        var ascending = string.IsNullOrWhiteSpace(orderByDescending);
        if (!ascending) orderBy = orderByDescending;
        var imagesRequest = new GetSpaceImages(ascending, page.Value, pageSize.Value, spaceId, customerId, orderBy);
        var spaceImagesResult = await mediator.Send(imagesRequest);
        if (!spaceImagesResult.SpaceExistsForCustomer || spaceImagesResult.PageOfAssets == null)
        {
            return HydraNotFound(spaceImagesResult.Errors?[0]);
        }
        var baseUrl = Request.GetBaseUrl();
        var resourceRoot = settings.DLCS.ResourceRoot.ToString();
        var collection = new HydraCollection<DLCS.HydraModel.Image>
        {
            WithContext = true,
            Members = spaceImagesResult.PageOfAssets.Assets
                .Select(a => a.ToHydra(baseUrl, resourceRoot)).ToArray(),
            TotalItems = spaceImagesResult.PageOfAssets.Total,
            PageSize = pageSize,
            Id = Request.GetJsonLdId()
        };
        PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value);
        return Ok(collection);
    }


    [HttpPatch]
    [ProducesResponseType(200, Type = typeof(HydraCollection<DLCS.HydraModel.Image>))]
    [ProducesResponseType(400, Type = typeof(Error))]
    public async Task<IActionResult> Images(
        [FromRoute] int customerId, [FromRoute] int spaceId,
        [FromBody] HydraCollection<DLCS.HydraModel.Image> images)
    {
        // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImages.cs#L147
            
        var patchedAssets = new List<Asset>();
            
        // Should there be a size limit on how many assets can be patched in a single go?
        if (images.Members is { Length: > 0 })
        {
            // Is it OK if a patched image has a different space? 
            // Yes, I think this is OK, it's a Move operation.
                
            if (images.Members.Any(image => image.ModelId == null))
            {
                // And this ModelId is NOT the customer/space/id construct that the DB has for a primary key.
                // It used to be... but we're not going to do that. hydraImage.ToDlcsModel() silently corrects this form.
                    
                return HydraProblem(
                    "All assets must have a ModelId", 
                    null, 400, "Missing identifier", null);
            }
            foreach (var hydraImage in images.Members)
            {
                try
                {
                    // Here a Hydra object is being passed into the MediatR layer.
                    // Should it get converted to a DLCS Model Asset first?
                    var dbAsset = hydraImage.ToDlcsModel(customerId);
                    var request = new PatchImage(customerId, spaceId, dbAsset);
                    var patched = await mediator.Send(request);
                    patchedAssets.Add(patched);
                }
                catch (APIException apiEx)
                {
                    return HydraProblem(
                        apiEx.Message, 
                        null, 500, apiEx.Label, null);
                }
            }
        }
            
        var baseUrl = Request.GetBaseUrl();
        var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            
        var output = new HydraCollection<DLCS.HydraModel.Image>
        {
            WithContext = true,
            Members = patchedAssets.Select(a => a.ToHydra(baseUrl, resourceRoot)).ToArray(),
            TotalItems = patchedAssets.Count,
            Id = Request.GetDisplayUrl() + "?patch_" + Guid.NewGuid()
        };
        return Ok(output);
    }
        
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(DLCS.HydraModel.Image))]
    [Route("{imageId}")]
    public async Task<DLCS.HydraModel.Image> Image(int customerId, int spaceId, string imageId)
    {
        var assetId = new AssetId(customerId, spaceId, imageId);
        var dbImage = await mediator.Send(new GetImage(assetId));
            
        var resourceRoot = settings.DLCS.ResourceRoot.ToString();
        var baseUrl = Request.GetBaseUrl();
        return dbImage.ToHydra(baseUrl, resourceRoot);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="spaceId"></param>
    /// <param name="imageId"></param>
    /// <param name="hydraAsset"></param>
    /// <returns></returns>
    /// <exception cref="BadRequestException"></exception>
    [ProducesResponseType(201, Type = typeof(DLCS.HydraModel.Image))]
    [ProducesResponseType(400, Type = typeof(ProblemDetails))]
    [HttpPut]
    [Route("{imageId}")]
    public async Task<IActionResult> Image(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] DLCS.HydraModel.Image hydraAsset)
    {
        // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L74
            
        var assetId = new AssetId(customerId, spaceId, imageId);
        var putAsset = hydraAsset.ToDlcsModel(customerId, spaceId, imageId);
        putAsset.Id = assetId.ToString();
            
        var request = new PutImage(putAsset);
        var putImageResult = await mediator.Send(request);
        if (putImageResult.Asset != null && putImageResult.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            var baseUrl = Request.GetBaseUrl();
            var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            var hydraResponse = putImageResult.Asset.ToHydra(baseUrl, resourceRoot);
            switch (putImageResult.StatusCode)
            {
                case HttpStatusCode.OK:
                    return Ok(hydraResponse);
                case HttpStatusCode.Created:
                    return Created(hydraResponse.Id, hydraResponse);
            }
        }
            
        return Problem(putImageResult.Message, putAsset.Id, 
            (int?)putImageResult.StatusCode, "PUT of Asset failed");

        // DISCUSS
        // now - how much of this logic happens here with multiple Mediatr requests?
        // Or is it all one PutImageRequest?
        // Same with the patch operation above. We can validate here in the controller that the space exists.
        // But should the Mediatr handler do that too? What if we called the Mediatr handler from somewhere else,
        // not an API controller? How much do we assume?

        // tbc.

        // DISCUSS
        // Controllers know about Hydra coming in and Mediatr commands going "out".
        // Controllers don't have references to repositories.
        // Mediatr requests/commands don't know about Hydra objects, they deal in DLCS.Model. 
        // Mediatr Handle methods don't make direct DBContext calls, they go to a repository.
        // (This is not consistent across what I've done so far, I don't know if it's right)
        // All three layers know about DLCS Model classes, but
        //  - the controllers just convert them to/from Hydra,
        //  - the Mediatr request package them to repository calls, sometimes multiple repositories
        //  - the repositories make db requests.

        // So the logic below lives in the Mediatr and we just have (cf SpaceController)
    }
}