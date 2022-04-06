using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Image.Requests;
using API.Features.Space.Requests;
using API.Settings;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Image
{
    [Route("/customers/{customerId}/spaces/{spaceId}/images/")]
    [ApiController]
    public class ImageController : Controller
    {
        private readonly IMediator mediator;
        private readonly ApiSettings settings;
        private readonly ILogger<ImageController> logger;

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
        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000, ValueLengthLimit = 100_000_000)]
        [Route("{imageId}")]
        public async Task<IActionResult> IngestBytes([FromRoute] string customerId, [FromRoute] string spaceId,
            [FromRoute] string imageId, [FromBody] ImageWithFile asset)
        {
            var claimsIdentity = User.Identity as ClaimsIdentity;
            var claim = claimsIdentity?.FindFirst("DlcsAuth").Value;
            var command = new IngestImageFromFile(customerId, spaceId, imageId,
                new MemoryStream(asset.File), asset.ToImage(), claim);

            var response = await mediator.Send(command);

            HttpStatusCode? statusCode = response.Value?.DownstreamStatusCode ??
                                         (response.Success
                                             ? HttpStatusCode.Created
                                             : HttpStatusCode.InternalServerError);

            return StatusCode((int) statusCode, response.Value?.Body);
        }
        
        
        [HttpGet]
        public async Task<HydraCollection<DLCS.HydraModel.Image>> Images(
            int customerId, int spaceId,
            int? page = 1, int? pageSize = -1,
            string? orderBy = null, string? orderByDescending = null)
        {
            if (pageSize < 0) pageSize = settings.PageSize;
            if (page < 0) page = 1;
            bool ascending = string.IsNullOrWhiteSpace(orderByDescending);
            if (!ascending) orderBy = orderByDescending;
            var baseUrl = Request.GetBaseUrl();
            var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            var imagesRequest = new GetSpaceImages(ascending, page.Value, pageSize.Value, spaceId, customerId, orderBy);
            var pageOfAssets = await mediator.Send(imagesRequest);
            
            var collection = new HydraCollection<DLCS.HydraModel.Image>
            {
                IncludeContext = true,
                Members = pageOfAssets.Assets.Select(a => a.ToHydra(baseUrl, resourceRoot)).ToArray(),
                TotalItems = pageOfAssets.Total,
                PageSize = pageSize,
                Id = Request.GetJsonLdId()
            };
            PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value);
            return collection;
        }


        [HttpPatch]
        public async Task<IActionResult> Images(
            int customerId, int spaceId,
            [FromBody] HydraCollection<DLCS.HydraModel.Image> images)
        {
            var patchedAssets = new List<Asset>();
            
            // Should there be a size limit on how many assets can be patched in a single go?
            if (images.Members != null && images.Members.Length > 0)
            {
                if (images.Members.Any(image => image.ModelId == null))
                {
                    return BadRequest(ErrorX.Create("Missing identifier","All assets must have a ModelId", 400));
                }
                foreach (var hydraImage in images.Members)
                {
                    try
                    {
                        var request = new PatchImage(customerId, spaceId, hydraImage.ModelId, hydraImage);
                        var patched = await mediator.Send(request);
                        patchedAssets.Add(patched);
                    }
                    catch (APIException apiEx)
                    {
                        return BadRequest(apiEx.ToHydra());
                    }
                }
            }
            
            var baseUrl = Request.GetBaseUrl();
            var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            
            var output = new HydraCollection<DLCS.HydraModel.Image>
            {
                IncludeContext = true,
                Members = patchedAssets.Select(a => a.ToHydra(baseUrl, resourceRoot)).ToArray(),
                TotalItems = patchedAssets.Count,
                Id = Request.GetDisplayUrl() + "?patch_" + Guid.NewGuid()
            };
            return Ok(output);
        }
        
        [HttpGet]
        [Route("{imageId}")]
        public async Task<DLCS.HydraModel.Image> Image(int customerId, int spaceId, string imageId)
        {
            var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            var baseUrl = Request.GetBaseUrl();
            var dbImage = await mediator.Send(new GetImage(customerId, spaceId, imageId));
            return dbImage.ToHydra(baseUrl, resourceRoot);
        }
    }
}