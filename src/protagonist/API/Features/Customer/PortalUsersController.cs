using System.Net;
using API.Features.Customer.Converters;
using API.Features.Customer.Requests;
using API.Infrastructure;
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
/// </summary>
[Route("/customers/")]
[ApiController]
public class PortalUsersController : HydraController
{
    /// <inheritdoc />
    public PortalUsersController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }

    /// <summary>
    /// GET /customers/{customerId}/portalUsers
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <returns>HydraCollection of PortalUser</returns>
    [HttpGet]
    [Route("{customerId}/portalUsers")]
    public async Task<HydraCollection<PortalUser>> GetPortalUsers(int customerId)
    {
        var users = await Mediator.Send(new GetPortalUsers(customerId));
            
        var baseUrl = GetUrlRoots().BaseUrl;
        var collection = new HydraCollection<PortalUser>
        {
            WithContext = true,
            Members = users.Select(s => s.ToHydra(baseUrl)).ToArray(),
            TotalItems = users.Count,
            PageSize = users.Count,
            Id = Request.GetJsonLdId()
        };
        return collection;
    }
        
    
    /// <summary>
    /// GET /customers/{customerId}/portalUsers/{userId}
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("{customerId}/portalUsers/{userId}")]
    public async Task<IActionResult> GetPortalUser(int customerId, string userId)
    {
        var users = await Mediator.Send(new GetPortalUsers(customerId));
        var user = users.SingleOrDefault(u => u.Id == userId);
        if (user != null)
        {
            return Ok(user.ToHydra(GetUrlRoots().BaseUrl));
        }

        return this.HydraNotFound();
    }
        
        
    /// <summary>
    /// POST /customers/{customerId}/portalUsers
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="portalUser"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("{customerId}/portalUsers")]
    public async Task<IActionResult> CreatePortalUser(int customerId, [FromBody] PortalUser portalUser)
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
        var result = await Mediator.Send(request);
        if (result.Error.HasText() || result.PortalUser == null)
        {
            return this.HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Cannot create user");
        }
            
        var hydraPortalUser = result.PortalUser.ToHydra(GetUrlRoots().BaseUrl);
        if (hydraPortalUser.Id.HasText())
        {
            return this.HydraCreated(hydraPortalUser);
        }
        return this.HydraProblem("No id on returned portal user", null, 500, "Cannot create user");
    }
        
        
    /// <summary>
    /// PATCH /customers/{customerId}/portalUsers/{userId}
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="userId"></param>
    /// <param name="portalUser"></param>
    /// <returns></returns>
    [HttpPatch]
    [Route("{customerId}/portalUsers/{userId}")]
    public async Task<IActionResult> PatchPortalUser(int customerId, string userId, [FromBody] PortalUser portalUser)
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
        var result = await Mediator.Send(request);
        if (result.Error.HasText() || result.PortalUser == null)
        {
            return this.HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Cannot Patch user");
        }
            
        var hydraPortalUser = result.PortalUser.ToHydra(GetUrlRoots().BaseUrl);
        return Ok(hydraPortalUser);
    }
        
        
    /// <summary>
    /// DELETE /customers/{customerId}/portalUsers/{userId}
    /// 
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("{customerId}/portalUsers/{userId}")]
    public async Task<IActionResult> DeletePortalUser(int customerId, string userId)
    {
        var result = await Mediator.Send(new DeletePortalUser(customerId, userId));
        if (result.Error.HasText())
        {
            return this.HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request");
        }

        return NoContent();
    }
    

}