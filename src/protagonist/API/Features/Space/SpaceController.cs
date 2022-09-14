using System;
using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Space.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Space;

/// <summary>
/// DLCS REST API Operations for Spaces.
/// </summary>
[Route("/customers/{customerId}/spaces")]
[ApiController]
public class SpaceController : HydraController
{
    private readonly ILogger<SpaceController> logger;

    /// <inheritdoc />
    public SpaceController(
        IMediator mediator,
        IOptions<ApiSettings> options,
        ILogger<SpaceController> logger) : base(options.Value, mediator)
    {
        this.logger = logger;
    }
    
    
    /// <summary>
    /// GET /customers/{customerId}/spaces
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="orderBy"></param>
    /// <param name="orderByDescending"></param>
    /// <returns>HydraCollection of Space</returns>
    [HttpGet]
    public async Task<HydraCollection<DLCS.HydraModel.Space>> GetSpaces(
        int customerId, int? page = 1, int? pageSize = -1, 
        string? orderBy = null, string? orderByDescending = null)
    {
        if (pageSize is null or < 0) pageSize = Settings.PageSize;
        if (page is null or < 0) page = 1;
        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);
        var baseUrl = GetUrlRoots().BaseUrl;
        var pageOfSpaces = await mediator.Send(new GetPageOfSpaces(
            page.Value, pageSize.Value, customerId, orderByField, descending));
        
        var collection = new HydraCollection<DLCS.HydraModel.Space>
        {
            WithContext = true,
            Members = pageOfSpaces.Spaces.Select(s => s.ToHydra(baseUrl)).ToArray(),
            TotalItems = pageOfSpaces.Total,
            PageSize = pageSize,
            Id = Request.GetJsonLdId()
        };
        PartialCollectionView.AddPaging(collection, page.Value, pageSize.Value, orderByField, descending);
        return collection;
    }


    /// <summary>
    /// POST /customers/{customerId}/spaces
    /// 
    /// Create a new space within this customer.
    /// DLCS assigns identity.
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="space"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateSpace(
        [FromRoute] int customerId, [FromBody] DLCS.HydraModel.Space space)
    {
        if (string.IsNullOrWhiteSpace(space.Name))
        {
            return this.HydraProblem("A space must have a name.", null, 400, "Invalid Space");
        }
        if (customerId <= 0)
        {
            return this.HydraProblem("Space must be created for an existing Customer.", null, 400, "Invalid Space");
        }
        
        logger.LogDebug("API will create space {SpaceName} for {CustomerId}", space.Name, customerId);

        var command = new CreateSpace(customerId, space.Name)
        {
            Roles = space.DefaultRoles,
            Tags = space.DefaultTags ?? Array.Empty<string>(),
            MaxUnauthorised = space.MaxUnauthorised
        };

        try
        {
            var newDbSpace = await mediator.Send(command);
            var newApiSpace = newDbSpace.ToHydra(GetUrlRoots().BaseUrl);
            if (newApiSpace.Id.HasText())
            {
                return this.HydraCreated(newApiSpace);
            }
            return this.HydraProblem("New space not assigned an ID", 
                null, 500, "Bad Request");
            
        }
        catch (BadRequestException badRequestException)
        {
            return this.HydraProblem(badRequestException.Message, 
                null, badRequestException.StatusCode, "Bad Request");
        }
    }
    
    /// <summary>
    /// GET /customers/{customerId}/spaces/{spaceId}
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="spaceId"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("{spaceId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Space))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSpace(int customerId, int spaceId)
    {
        var dbSpace = await mediator.Send(new GetSpace(customerId, spaceId));
        if (dbSpace != null)
        {
            return Ok(dbSpace.ToHydra(GetUrlRoots().BaseUrl));
        }

        return NotFound();
    }
    
    
    /// <summary>
    /// PATCH /customers/{customerId}/spaces/{spaceId}
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="spaceId"></param>
    /// <param name="space"></param>
    /// <returns></returns>
    [HttpPatch]
    [Route("{spaceId}")]
    public async Task<IActionResult> PatchSpace(
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
            return Ok(result.Space.ToHydra(GetUrlRoots().BaseUrl));
        }
        
        if (result.Conflict)
        {
            return this.HydraProblem(result.ErrorMessages, null, 409, "Space name taken");
        }
        return this.HydraProblem(result.ErrorMessages, null, 500, "Cannot patch space");
    }
}