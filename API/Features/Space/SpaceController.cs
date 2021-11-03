using System;
using System.Linq;
using System.Threading.Tasks;
using API.Client;
using API.Converters;
using API.Features.Space.Requests;
using API.Settings;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Space
{
    [Route("/customers/{customerId}/spaces")]
    [ApiController]
    public class SpaceController : Controller
    {
        private readonly IMediator mediator;
        private readonly ApiSettings settings;

        public SpaceController(
            IMediator mediator,
            IOptions<ApiSettings> options)
        {
            this.mediator = mediator;
            settings = options.Value;
        }
        
        
        [HttpGet]
        public async Task<HydraCollection<DLCS.HydraModel.Space>> Index(int customerId, int? page = 1, int? pageSize = -1, string? orderBy = null)
        {
            if (pageSize < 0) pageSize = settings.PageSize;
            if (page < 0) page = 1;
            var baseUrl = Request.GetBaseUrl();
            var pageOfSpaces = await mediator.Send(new GetPageOfSpaces(page.Value, pageSize.Value, customerId, orderBy));
            
            var collection = new HydraCollection<DLCS.HydraModel.Space>
            {
                IncludeContext = true,
                Members = pageOfSpaces.Spaces.Select(s => s.ToHydra(baseUrl)).ToArray(),
                TotalItems = pageOfSpaces.Total,
                PageSize = pageSize,
                Id = Request.GetJsonLdId()
            };
            PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value);
            return collection;
        }

        /// <summary>
        /// Create a new space within this customer.
        /// DLCS assigns identity.
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="space"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Index(
            int customerId, [FromBody] DLCS.HydraModel.Space space)
        {
            if (string.IsNullOrWhiteSpace(space.Name))
            {
                return BadRequest("A space must have a name.");
            }
            if (customerId <= 0)
            {
                return BadRequest("Space must be created for an existing Customer.");
            }
            
            var command = new CreateSpace(customerId, space.Name)
            {
                // ImageBucket = space.ImageBucket, // not there
                Roles = space.DefaultRoles,
                Tags = string.Join(",", space.DefaultTags ?? Array.Empty<string>()),
                MaxUnauthorised = space.MaxUnauthorised
            };

            try
            {
                var newDbSpace = await mediator.Send(command);
                var newApiSpace = newDbSpace.ToHydra(Request.GetBaseUrl());
                return Created(newApiSpace.Id, newApiSpace);
            }
            catch (BadRequestException badRequestException)
            {
                // Are exceptions the way this info should be passed back to the controller?
                return BadRequest(badRequestException.Message);
            }
        }
        
        
        
        [HttpGet]
        [Route("{spaceId}")]
        public async Task<DLCS.HydraModel.Space> Index(int customerId, int spaceId)
        {
            var baseUrl = Request.GetBaseUrl();
            var dbSpace = await mediator.Send(new GetSpace(customerId, spaceId));
            return dbSpace.ToHydra(baseUrl);
        }
        
        
        [HttpGet]
        [Route("{spaceId}/images")]
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
            var imagesRequest = new GetSpaceImages(ascending, page.Value, pageSize.Value, spaceId, customerId, orderBy);
            var pageOfAssets = await mediator.Send(imagesRequest);
            
            var collection = new HydraCollection<DLCS.HydraModel.Image>
            {
                IncludeContext = true,
                Members = pageOfAssets.Assets.Select(a => a.ToHydra(baseUrl, settings.DLCS.ResourceRoot.ToString())).ToArray(),
                TotalItems = pageOfAssets.Total,
                PageSize = pageSize,
                Id = Request.GetJsonLdId()
            };
            PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value);
            return collection;
        }
    }

}