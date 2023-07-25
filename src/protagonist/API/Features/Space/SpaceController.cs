using API.Exceptions;
using API.Features.Space.Converters;
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
    /// Get details of all spaces for customer.
    /// </summary>
    /// <returns>HydraCollection of Space</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<HydraCollection<DLCS.HydraModel.Space>> GetSpaces(
        int customerId, int? page = 1, int? pageSize = -1, 
        string? orderBy = null, string? orderByDescending = null)
    {
        if (pageSize is null or < 0) pageSize = Settings.PageSize;
        if (page is null or < 0) page = 1;
        var orderByField = this.GetOrderBy(orderBy, orderByDescending, out var descending);
        var baseUrl = GetUrlRoots().BaseUrl;
        var pageOfSpaces = await Mediator.Send(new GetPageOfSpaces(
            page.Value, pageSize.Value, customerId, orderByField, descending));
        
        var collection = new HydraCollection<DLCS.HydraModel.Space>
        {
            WithContext = true,
            Members = pageOfSpaces.Spaces.Select(s => s.ToHydra(baseUrl)).ToArray(),
            TotalItems = pageOfSpaces.Total,
            PageSize = pageSize,
            Id = Request.GetJsonLdId()
        };
        PartialCollectionView.AddPaging(collection, new PartialCollectionViewPagingValues
        {
            Page = page.Value, PageSize = pageSize.Value, OrderBy = orderByField, Descending = descending
        });
        return collection;
    }

    /// <summary>
    /// Create a new space within this customer. DLCS assigns identity.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/spaces
    ///     {
    ///         "@type":"Space",
    ///         "name":"foo"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            var newDbSpace = await Mediator.Send(command);
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
    /// Delete a specified customers space
    /// </summary>
    [HttpDelete]
    [Route("{spaceId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Space))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSpace(int customerId, int spaceId)
    {
        var deleteRequest = new DeleteSpace(customerId, spaceId);

        return await HandleDelete<IActionResult>(deleteRequest);
    }
    
    /// <summary>
    /// Get details of specified customers space
    /// </summary>
    [HttpGet]
    [Route("{spaceId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Space))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSpace(int customerId, int spaceId)
    {
        var dbSpace = await Mediator.Send(new GetSpace(customerId, spaceId));
        if (dbSpace != null)
        {
            return Ok(dbSpace.ToHydra(GetUrlRoots().BaseUrl));
        }

        return NotFound();
    }

    /// <summary>
    /// Make a partial update of an existing space
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PATCH: /customers/1/spaces/5
    ///     {
    ///         "@type": "Space",
    ///         "name": "New Space Name"
    ///     }
    /// </remarks>
    [HttpPatch]
    [Route("{spaceId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Space))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        
        var result = await Mediator.Send(patchSpace);
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