using System;
using System.Linq;
using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace DLCS.Mock.Controllers;

[ApiController]
public class CustomersController : ControllerBase
{
    private readonly MockModel model;
    
    public CustomersController(
        MockModel model)
    {
        this.model = model;
    }
    
    [HttpGet]
    [Route("/customers")]
    public HydraCollection<JObject> Index()
    {
        var customers = model.Customers
            .Select(c => c.GetCollectionForm()).ToArray();

        return new HydraCollection<JObject>
        {
            WithContext = true,
            Members = customers,
            TotalItems = customers.Length,
            Id = Request.GetDisplayUrl()
        };
    }


    [HttpGet]
    [Route("/customers/{customerId}")]
    public IActionResult Index(int customerId)
    {
        var customer = model.Customers.SingleOrDefault(c => c.ModelId == customerId);
        if (customer == null)
        {
            return NotFound();
        }
        return Ok(customer);
    }

    [HttpGet]
    [Route("/customers/{customerId}/portalUsers")]
    public HydraCollection<JObject> PortalUsers(int customerId)
    {
        var portalUsers = model.PortalUsers
            .Where(p => p.CustomerId == customerId)
            .Select(p => p.GetCollectionForm()).ToArray();

        return new HydraCollection<JObject>
        {
            WithContext = true,
            Members = portalUsers,
            TotalItems = portalUsers.Length,
            Id = Request.GetDisplayUrl()
        };
    }

    [HttpGet]
    [Route("/customers/{customerId}/portalUsers/{portalUserId}")]
    public IActionResult PortalUsers(int customerId, string portalUserId)
    {
        var user = model.PortalUsers.SingleOrDefault(
            u => u.CustomerId == customerId && u.ModelId == portalUserId);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }


    [HttpGet]
    [Route("/customers/{customerId}/originStrategies")]
    public HydraCollection<CustomerOriginStrategy> OriginStrategies(int customerId)
    {
        var customerOriginStrategies = model.CustomerOriginStrategies
            .Where(os => os.CustomerId == customerId)
            .ToArray();

        return new HydraCollection<CustomerOriginStrategy>
        {
            WithContext = true,
            Members = customerOriginStrategies,
            TotalItems = customerOriginStrategies.Length,
            Id = Request.GetDisplayUrl()
        };

    }

    [HttpGet]
    [Route("/customers/{customerId}/originStrategies/{originStrategyId}")]
    public IActionResult OriginStrategies(int customerId, int originStrategyId)
    {
        var cos = model.CustomerOriginStrategies.SingleOrDefault(
            u => u.CustomerId == customerId && u.ModelId == originStrategyId);
        if (cos == null)
        {
            return NotFound();
        }
        return Ok(cos);
    }

    [HttpGet]
    [Route("/customers/{customerId}/spaces")]
    public HydraCollection<JObject> Spaces(int customerId)
    {
        var spaces = model.Spaces
            .Where(p => p.CustomerId == customerId)
            .Select(p => p.GetCollectionForm()).ToArray();

        return new HydraCollection<JObject>
        {
            WithContext = true,
            Members = spaces,
            TotalItems = spaces.Length,
            Id = Request.GetDisplayUrl()
        };
    }

    [HttpPost]
    [Route("/customers/{customerId}/spaces")]
    public IActionResult Spaces(int customerId, Space space)
    {
        if (!string.IsNullOrWhiteSpace(space.Id))
        {
            return Conflict("You can only POST a new Space");
        }
        if (string.IsNullOrWhiteSpace(space.Name))
        {
            return BadRequest("The space must be given a name");
        }
        // obviously not thread safe..
        var modelId = model.Spaces.Select(s => s.ModelId).Max() + 1;
        var newSpace = MockHelp.MakeSpace(model.BaseUrl, modelId ?? 0, customerId,
            space.Name, DateTime.UtcNow, space.DefaultTags, space.MaxUnauthorised);
        model.Spaces.Add(newSpace);
        return Created(newSpace.Id, space);
    }
    
    [HttpGet]
    [Route("/customers/{customerId}/spaces/{spaceId}")]
    public IActionResult Spaces(int customerId, int spaceId)
    {
        var space = model.Spaces.SingleOrDefault(s => s.CustomerId == customerId && s.ModelId == spaceId);
        if (space == null)
        {
            return NotFound();
        }
        return Ok(space);
    }

}
