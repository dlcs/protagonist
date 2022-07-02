using System;
using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Space.Requests;
using API.Settings;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Space
{
    [Route("/customers/{customerId}/spaces")]
    [ApiController]
    public class SpaceController : HydraController
    {
        private readonly IMediator mediator;
        private readonly ILogger<SpaceController> logger;

        public SpaceController(
            IMediator mediator,
            IOptions<ApiSettings> options,
            ILogger<SpaceController> logger) : base(options.Value)
        {
            this.mediator = mediator;
            this.logger = logger;
        }
        
        
        [HttpGet]
        public async Task<HydraCollection<DLCS.HydraModel.Space>> Index(
            int customerId, int? page = 1, int? pageSize = -1, 
            string? orderBy = null, bool ascending = true)
        {
            if (pageSize < 0) pageSize = Settings.PageSize;
            if (page < 0) page = 1;
            var baseUrl = getUrlRoots().BaseUrl;
            var pageOfSpaces = await mediator.Send(new GetPageOfSpaces(
                page.Value, pageSize.Value, customerId, orderBy, ascending));
            
            var collection = new HydraCollection<DLCS.HydraModel.Space>
            {
                WithContext = true,
                Members = pageOfSpaces.Spaces.Select(s => s.ToHydra(baseUrl)).ToArray(),
                TotalItems = pageOfSpaces.Total,
                PageSize = pageSize,
                Id = Request.GetJsonLdId()
            };
            PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value, orderBy, ascending);
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
            [FromRoute] int customerId, [FromBody] DLCS.HydraModel.Space space)
        {
            logger.LogInformation("API will create a space");
            if (string.IsNullOrWhiteSpace(space.Name))
            {
                return HydraProblem("A space must have a name.", null, 400, "Invalid Space", null);
            }
            if (customerId <= 0)
            {
                return HydraProblem("Space must be created for an existing Customer.", null, 400, "Invalid Space", null);
            }
            
            var command = new CreateSpace(customerId, space.Name)
            {
                Roles = space.DefaultRoles,
                Tags = space.DefaultTags ?? Array.Empty<string>(),
                MaxUnauthorised = space.MaxUnauthorised
            };

            try
            {
                var newDbSpace = await mediator.Send(command);
                var newApiSpace = newDbSpace.ToHydra(getUrlRoots().BaseUrl);
                return Created(newApiSpace.Id, newApiSpace);
            }
            catch (BadRequestException badRequestException)
            {
                // Are exceptions the way this info should be passed back to the controller?
                return HydraProblem(badRequestException.Message, 
                    null, badRequestException.StatusCode, "Bad Request", null);
            }
        }
        
        [HttpGet]
        [Route("{spaceId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Space))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Index(int customerId, int spaceId)
        {
            var dbSpace = await mediator.Send(new GetSpace(customerId, spaceId));
            if (dbSpace != null)
            {
                return Ok(dbSpace.ToHydra(getUrlRoots().BaseUrl));
            }

            return NotFound();
        }
        
        [HttpPatch]
        [Route("{spaceId}")]
        public async Task<IActionResult> Patch(
            int customerId, int spaceId, [FromBody] DLCS.HydraModel.Space space)
        {
            var patchSpace = new PatchSpace
            {
                CustomerId = customerId,
                SpaceId = spaceId,
                Name = space.Name,
                MaxUnauthorised = space.MaxUnauthorised,
                Tags = space.DefaultTags,
                Roles = space.DefaultRoles
            };
            
            var result = await mediator.Send(patchSpace);
            if (!result.ErrorMessages.Any() && result.Space != null)
            {
                return Ok(result.Space.ToHydra(getUrlRoots().BaseUrl));
            }
            
            if (result.Conflict)
            {
                return HydraProblem(result.ErrorMessages, null, 409, "Space name taken", null);
            }
            return HydraProblem(result.ErrorMessages, null, 500, "Cannot patch space", null);
        }
        
        
    }

}