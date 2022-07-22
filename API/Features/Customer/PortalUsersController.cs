using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Customer.Requests;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Model.Customers;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Customer;

/// <summary>
/// DLCS REST API Operations for Portal Users.
/// This controller does not do any data access; it creates Mediatr requests and passes them on.
/// It converts to and from the Hydra form of the DLCS API.
/// </summary>
[Route("/customers/")]
[ApiController]
public class PortalUsersController : HydraController
{
    private readonly IMediator mediator;

    /// <inheritdoc />
    public PortalUsersController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value)
    {
        this.mediator = mediator;
    }
    
    
        
    // ################# GET /customers/id/portalUsers #####################
    [HttpGet]
    [Route("{customerId}/portalUsers")]
    public async Task<HydraCollection<PortalUser>> GetUsers(int customerId)
    {
        var users = await mediator.Send(new GetPortalUsers { CustomerId = customerId });
            
        var baseUrl = getUrlRoots().BaseUrl;
        var collection = new HydraCollection<DLCS.HydraModel.PortalUser>
        {
            WithContext = true,
            Members = users.Select(s => s.ToHydra(baseUrl)).ToArray(),
            TotalItems = users.Count,
            PageSize = users.Count,
            Id = Request.GetJsonLdId()
        };
        return collection;
    }
        
    // ################# GET /customers/id/portalUsers/id #####################
    [HttpGet]
    [Route("{customerId}/portalUsers/{userId}")]
    public async Task<IActionResult> GetUser(int customerId, string userId)
    {
        var users = await mediator.Send(new GetPortalUsers { CustomerId = customerId });
        var user = users.SingleOrDefault(u => u.Id == userId);
        if (user != null)
        {
            return Ok(user.ToHydra(getUrlRoots().BaseUrl));
        }

        return HydraNotFound();
    }
        
        
    // ################# POST /customers/id/portalUsers #####################
    [HttpPost]
    [Route("{customerId}/portalUsers")]
    public async Task<IActionResult> CreateUser(int customerId, [FromBody] PortalUser portalUser)
    {
        var request = new CreatePortalUser
        {
            Password = portalUser.Password,
            PortalUser = new User
            {
                Customer = customerId,
                Email = portalUser.Email
            }
        };
        var result = await mediator.Send(request);
        if (result.Error.HasText() || result.PortalUser == null)
        {
            return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Cannot create user", null);
        }
            
        var hydraPortalUser = result.PortalUser.ToHydra(getUrlRoots().BaseUrl);
        return Created(hydraPortalUser.Id, hydraPortalUser);
    }
        
        
    // ################# PATCH /customers/id/portalUsers/id #####################
    [HttpPatch]
    [Route("{customerId}/portalUsers/{userId}")]
    public async Task<IActionResult> PatchUser(int customerId, string userId, [FromBody] PortalUser portalUser)
    {
        // NB Deliverator doesn't support toggling Enabled here so we won't for now.
            
        var request = new PatchPortalUser
        {
            Password = portalUser.Password,
            PortalUser = new User
            {
                Id = userId,
                Customer = customerId,
                Email = portalUser.Email
            }
        };
        var result = await mediator.Send(request);
        if (result.Error.HasText() || result.PortalUser == null)
        {
            return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Cannot Patch user", null);
        }
            
        var hydraPortalUser = result.PortalUser.ToHydra(getUrlRoots().BaseUrl);
        return Ok(hydraPortalUser);
    }
        
        
    // ################# DELETE /customers/id/portalUsers/id #####################
    [HttpDelete]
    [Route("{customerId}/portalUsers/{userId}")]
    public async Task<IActionResult> DeleteUser(int customerId, string userId)
    {
        var result = await mediator.Send(new DeletePortalUser(customerId, userId));
        if (result.Error.HasText())
        {
            return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request", null);
        }

        return NoContent();
    }
    

}