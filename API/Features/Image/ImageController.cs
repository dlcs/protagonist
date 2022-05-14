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
using DLCS.Core.Types;
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
    [Route("/customers/{customerId}/spaces/{spaceId}/images")]
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
        [HttpPost]  // This should be a PUT? But then it will be the same op to same location as a normal asset without File.
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
            var imagesRequest = new GetSpaceImages(ascending, page.Value, pageSize.Value, spaceId, customerId, orderBy);
            var pageOfAssets = await mediator.Send(imagesRequest);
            
            var baseUrl = Request.GetBaseUrl();
            var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            var collection = new HydraCollection<DLCS.HydraModel.Image>
            {
                WithContext = true,
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
            [FromRoute] int customerId, [FromRoute] int spaceId,
            [FromBody] HydraCollection<DLCS.HydraModel.Image> images)
        {
            // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImages.cs#L147
            
            var patchedAssets = new List<Asset>();
            
            // Should there be a size limit on how many assets can be patched in a single go?
            if (images.Members is { Length: > 0 })
            {
                if (images.Members.Any(image => image.CustomerId != customerId))
                {
                    return BadRequest(ErrorX.Create("Wrong customer", 
                "At least one supplied image does not have the correct customer Id", 400))
;               }
                // Is it OK if a patched image has a different space? 
                // Yes, I think this is OK, it's a Move operation.
                
                if (images.Members.Any(image => image.ModelId == null))
                {
                    // And this ModelId is NOT the customer/space/id construct that the DB has for a primary key.
                    // It used to be... but we're not going to do that.
                    // Test if ModelId starts with cust/space and throw if it does?
                    return BadRequest(ErrorX.Create("Missing identifier", 
                        "All assets must have a ModelId", 400));
                }
                foreach (var hydraImage in images.Members)
                {
                    try
                    {
                        // Here a Hydra object is being passed into the MediatR layer.
                        // Should it get converted to a DLCS Model Asset first?
                        var dbAsset = hydraImage.ToDlcsModel();
                        var request = new PatchImage(customerId, spaceId, dbAsset);
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
                WithContext = true,
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
            var assetId = new AssetId(customerId, spaceId, imageId);
            var dbImage = await mediator.Send(new GetImage(assetId));
            
            var resourceRoot = settings.DLCS.ResourceRoot.ToString();
            var baseUrl = Request.GetBaseUrl();
            return dbImage.ToHydra(baseUrl, resourceRoot);
        }

        [ProducesResponseType(201, Type = typeof(DLCS.HydraModel.Image))]
        [ProducesResponseType(400, Type = typeof(ProblemDetails))]
        [HttpPut]
        [Route("{imageId}")]
        public async Task<DLCS.HydraModel.Image> Image(
            [FromRoute] int customerId,
            [FromRoute] int spaceId,
            [FromRoute] string imageId,
            [FromBody] DLCS.HydraModel.Image hydraAsset)
        {
            // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L74
            
            var assetId = new AssetId(customerId, spaceId, imageId);
            var putAsset = hydraAsset.ToDlcsModel();
            putAsset.Id = assetId.ToString();
            // It has to have that ID! That's where it's being put, regardless of the incoming Hydra object's Id.
            // Shall we validate that the incoming object is the same ID?
            if (!hydraAsset.Id.EndsWith(putAsset.Id))
            {
                throw new BadRequestException(
                    $"Incoming asset Id '{hydraAsset.Id}' does not match PUT URL '{Request.GetDisplayUrl()}'");
            }

            
            // now - how much of this logic happens here with multiple Mediatr requests?
            // Or is it all one PutImageRequest?
            
            
            // tbc.
            
            
            // We are going to need to have cached versions of all these policies, but just memoryCache for API I think


            // Deliverator Logic:
            // LoadSpaceBehaviour - need the space to exist (space cache?)
            // LoadImageBehaviour - see if already exists
            // LoadCustomerStorageBehaviour - if a new image, CustomerStorageCalculation
            // LoadStoragePolicyBehaviour - get the storage policy
            // EnforceStoragePolicyForNumberOfImagesBehaviour
            // ValidateImageUpsertBehaviour
            // Load default ImageOptimisationPolicies and ThumbnailPolicies
            // LoadAllImageOptimisationPoliciesBehaviour
            // LoadAllThumbnailPoliciesBehaviour
            // SelectImageOptimisationPolicyForImageBehaviour - whether image, audio or video
            // SelectThumbnailPolicyForImageBehaviour - image, audio or video
            // UpdateImageBehaviour - store in DB
            // CreateSkeletonImageLocationBehaviour
            // UpdateImageLocationBehaviour
            // if Image:
            // CallImageIngestEndpointBehaviour
            // LoadImageBehaviour (reload)
            // return 200 or 201 or 500
            // else




        }
    }
}