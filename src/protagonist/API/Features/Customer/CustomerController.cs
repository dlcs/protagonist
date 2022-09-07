using System;
using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Customer.Requests;
using API.Features.Customer.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.Web.Auth;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace API.Features.Customer;

/// <summary>
/// DLCS REST API Operations for customers.
/// </summary>
/// <remarks>
/// This controller does not do any data access; it creates Mediatr requests and passes them on.
/// It converts to and from the Hydra form of the DLCS API.
/// </remarks>
[Route("/customers/")]
[ApiController]
public class CustomerController : HydraController
{
    private readonly IMediator mediator;

    /// <inheritdoc />
    public CustomerController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value)
    {
        this.mediator = mediator;
    }

    /// <summary>
    /// GET /customers
    /// 
    /// Get all the customers.
    /// </summary>
    /// <returns>HydraCollection of JObject (simplified customer)</returns>
    /// <remarks>
    /// Although it returns a paged collection, the page size is always the total number of customers:
    /// clients don't need to page this collection, it contains all customers.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet]
    public async Task<HydraCollection<JObject>> GetCustomers()
    {
        var baseUrl = GetUrlRoots().BaseUrl;
        var dbCustomers = await mediator.Send(new GetAllCustomers());
            
        return new HydraCollection<JObject>
        {
            WithContext = true,
            Members = dbCustomers.Select(c => c.ToCollectionForm(baseUrl)).ToArray(),
            TotalItems = dbCustomers.Count,
            PageSize = dbCustomers.Count,
            Id = Request.GetJsonLdId()
        };
    }

        
    /// <summary>
    /// POST /customers
    /// 
    /// The /customers/ path is not access controlled, but only an admin may call this.
    /// </summary>
    /// <param name="newCustomer"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] DLCS.HydraModel.Customer newCustomer)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }
            
        var basicErrors = HydraCustomerValidator.GetNewHydraCustomerErrors(newCustomer);
        if (basicErrors.Any())
        {
            return HydraProblem(basicErrors, null, 400, "Invalid Customer");
        }

        var command = new CreateCustomer(newCustomer.Name!, newCustomer.DisplayName!);

        try
        {
            var result = await mediator.Send(command);
            if (result.Customer == null || result.ErrorMessages.Any())
            {
                int statusCode = result.Conflict ? 409 : 500;
                return HydraProblem(result.ErrorMessages, null, statusCode, "Could not create Customer");
            }
            var newApiCustomer = result.Customer.ToHydra(GetUrlRoots().BaseUrl);
            if (newApiCustomer.Id.HasText())
            {
                return Created(newApiCustomer.Id, newApiCustomer);
            }
            return HydraProblem("No ID assigned for new customer", null, 500, "Could not create Customer");
        }
        catch (Exception ex)
        {
            // Are exceptions the way this info should be passed back to the controller?
            return HydraProblem(ex);
        }
    }
        
        
    /// <summary>
    /// GET /customers/{id}
    /// 
    /// Get a Customer
    /// </summary>
    /// <param name="customerId"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("{customerId}")]
    public async Task<IActionResult> GetCustomer(int customerId)
    {
        var dbCustomer = await mediator.Send(new GetCustomer(customerId));
        if (dbCustomer == null)
        {
            return HydraNotFound();
        }
        return Ok(dbCustomer.ToHydra(GetUrlRoots().BaseUrl));
    }

    /// <summary>
    /// PATCH /customers/{id}
    /// 
    /// Make a partial update to customer.
    /// </summary>
    /// <param name="customerId">Id of customer to Patch</param>
    /// <param name="hydraCustomer">Hydra model containing changes to make (only DisplayName is supported)</param>
    /// <param name="validator">Model validator</param>
    /// <returns>The updated Customer entity</returns>
    [HttpPatch]
    [Route("{customerId}")]
    public async Task<IActionResult> PatchCustomer(
        [FromRoute] int customerId,
        [FromBody] DLCS.HydraModel.Customer hydraCustomer,
        [FromServices] CustomerPatchValidator validator)
    {
        var validationResult = await validator.ValidateAsync(hydraCustomer);
        if (!validationResult.IsValid)
        {
            return ValidationFailed(validationResult);
        }

        var request = new PatchCustomer(customerId, hydraCustomer.DisplayName!);
        var result = await mediator.Send(request);

        switch (result.UpdateResult)
        {
            case UpdateResult.Updated:
                return Ok(result.Entity!.ToHydra(GetUrlRoots().BaseUrl));
            case UpdateResult.NotFound:
                return HydraNotFound();
            case UpdateResult.Error:
                return HydraProblem(result.Error, null, 500, "Error patching customer");
        }

        return HydraProblem("Unknown result", null, 500, "Unknown result");
    }
}