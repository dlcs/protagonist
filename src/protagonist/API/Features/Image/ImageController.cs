using System.Net;
using System.Threading;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Image.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Image;

/// <summary>
/// Controller for handling requests for image (aka Asset) resources
/// </summary>
[Route("/customers/{customerId}/spaces/{spaceId}/images/{imageId}")]
[ApiController]
public class ImageController : HydraController
{
    private readonly IMediator mediator;

    /// <inheritdoc />
    public ImageController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value)
    {
        this.mediator = mediator;
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
    public async Task<IActionResult> PutImage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        [FromBody] DLCS.HydraModel.Image hydraAsset)
    {
        return await PutOrPatchAsset(customerId, spaceId, imageId, hydraAsset);
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
        [FromBody] DLCS.HydraModel.Image hydraAsset)
    {
        return await PutOrPatchAsset(customerId, spaceId, imageId, hydraAsset);
    }

    /// <summary>
    /// DELETE /customers/{customerId}/spaces/{spaceId}/images/{imageId}
    ///
    /// DELETE asset at specified location. This will remove asset immediately, generated derivatives will be picked up
    /// and processed eventually. 
    /// </summary>
    /// <param name="customerId">(from resource path)</param>
    /// <param name="spaceId">(from resource path)</param>
    /// <param name="imageId">(from resource path)</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns></returns>
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [HttpDelete]
    public async Task<IActionResult> DeleteAsset([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, CancellationToken cancellationToken)
    {
        var deleteRequest = new DeleteAsset(customerId, spaceId, imageId);
        var result = await mediator.Send(deleteRequest, cancellationToken);

        return result switch
        {
            DeleteResult.NotFound => NotFound(),
            DeleteResult.Error => HydraProblem("Error deleting asset - delete failed", null, 500,
                "Delete Asset failed"),
            _ => NoContent()
        };
    }
    
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
    public async Task<IActionResult> PostImageWithFileBytes([FromRoute] int customerId, [FromRoute] int spaceId,
        [FromRoute] string imageId, [FromBody] ImageWithFile asset)
    {
        const string errorTitle = "POST of Asset bytes failed";
        var assetId = new AssetId(customerId, spaceId, imageId);
        if (asset.File == null || asset.File.Length == 0)
        {
            return HydraProblem("No file bytes in request body", assetId.ToString(),
                (int?)HttpStatusCode.BadRequest, errorTitle);
        }
        if (asset.MediaType.IsNullOrEmpty())
        {
            return HydraProblem("MediaType must be supplied", assetId.ToString(),
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
            return HydraProblem("Could not save uploaded file", assetId.ToString(), 500, errorTitle);
        }

        asset.Origin = result.Origin;
        asset.File = null;

        return await PutOrPatchAsset(customerId, spaceId, imageId, asset);
    }

    private async Task<IActionResult> PutOrPatchAsset(int customerId, int spaceId, string imageId,
        DLCS.HydraModel.Image hydraAsset)
    {
        var assetId = new AssetId(customerId, spaceId, imageId);
        var asset = hydraAsset.ToDlcsModel(customerId, spaceId, imageId);
        asset.Id = assetId.ToString();

        // In the special case where we were passed ImageWithFile from the PostImageWithFileBytes action, 
        // it was a POST - but we should revisit that as the direct image ingest should be a PUT as well I think
        // See https://github.com/dlcs/protagonist/issues/338
        var method = hydraAsset is ImageWithFile ? "PUT" : Request.Method;

        var request = new CreateOrUpdateImage(asset, method);
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
                return HydraProblem("No ID in returned Image", asset.Id,
                    (int?)result.StatusCode, "PUT or PATCH of Asset failed");
            }
        }

        return HydraProblem(result.Message, asset.Id,
            (int?)result.StatusCode, "PUT or PATCH of Asset failed");
    }
}