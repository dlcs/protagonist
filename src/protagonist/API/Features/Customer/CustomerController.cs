using API.Features.Customer.Converters;
using API.Features.Customer.Requests;
using API.Features.Customer.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.Web.Auth;
using DLCS.Web.Requests;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace API.Features.Customer;

/// <summary>
/// DLCS REST API Operations for customers.
/// </summary>
[Route("/customers/")]
[ApiController]
public class CustomerController : HydraController
{
    public CustomerController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }

    /// <summary>
    /// Get all the customers.
    /// </summary>
    /// <returns>HydraCollection of simplified customer</returns>
    /// <remarks>
    /// Although it returns a paged collection, the page size is always the total number of customers:
    /// clients don't need to page this collection, it contains all customers.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<HydraCollection<JObject>> GetCustomers()
    {
        var baseUrl = GetUrlRoots().BaseUrl;
        var dbCustomers = await Mediator.Send(new GetAllCustomers());
            
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
    /// Create a new Customer.
    /// 
    /// Only an admin may call this.
    /// </summary>
    /// <param name="newCustomer">Object containing new customer to create</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers
    ///     {
    ///         "Name": "new-url-friendly-name"
    ///         "DisplayName": "Display Name"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(Error))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(Error))]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] DLCS.HydraModel.Customer newCustomer,
        [FromServices] HydraCustomerValidator validator,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }
        
        var validationResult = await validator.ValidateAsync(newCustomer);
        if (!validationResult.IsValid)
        {
            return this.HydraProblem(validationResult.Errors.Select(s => s.ErrorMessage), null, 400, "Invalid Customer");
        }
        
        return await HandleUpsert(new CreateCustomer(newCustomer.Name!, newCustomer.DisplayName!),
            c => c.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Could not create Customer",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get details of specified customer
    /// </summary>
    [HttpGet]
    [Route("{customerId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Customer))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetCustomer(int customerId)
    {
        var dbCustomer = await Mediator.Send(new GetCustomer(customerId));
        if (dbCustomer == null)
        {
            return this.HydraNotFound();
        }
        return Ok(dbCustomer.ToHydra(GetUrlRoots().BaseUrl));
    }

    /// <summary>
    /// Make a partial update to customer.
    /// Note: Only the DisplayName property can be updated
    /// </summary>
    /// <param name="customerId">Id of customer to Patch</param>
    /// <param name="hydraCustomer">Hydra model containing changes to make (only DisplayName is supported)</param>
    /// <param name="validator">Model validator</param>
    /// <returns>The updated Customer entity</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     PATCH: /customers/100
    ///     {
    ///         "DisplayName": "Updated Display Name"
    ///     }
    /// </remarks>
    [HttpPatch]
    [Route("{customerId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.Customer))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    public async Task<IActionResult> PatchCustomer(
        [FromRoute] int customerId,
        [FromBody] DLCS.HydraModel.Customer hydraCustomer,
        [FromServices] CustomerPatchValidator validator)
    {
        var validationResult = await validator.ValidateAsync(hydraCustomer);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var request = new PatchCustomer(customerId, hydraCustomer.DisplayName!);

        return await HandleUpsert(
            request, 
            customer => customer.ToHydra(GetUrlRoots().BaseUrl), 
            customerId.ToString(),
            "Patch failed");
    }
}