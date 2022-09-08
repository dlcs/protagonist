using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Features.Customer.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Customer;

/// <summary>
/// DLCS REST API Operations for API Keys.
/// This controller does not do any data access; it creates Mediatr requests and passes them on.
/// It converts to and from the Hydra form of the DLCS API.
/// </summary>
[Route("/customers/")]
[ApiController]
public class ApiKeysController : HydraController
{
    /// <inheritdoc />
    public ApiKeysController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    /// <summary>
    /// GET /customers/id/keys
    ///
    /// List of all the API Keys available for this customer
    /// </summary>
    /// <param name="customerId"></param>
    /// <returns>HydraCollection of ApiKey objects</returns>
    [HttpGet]
    [Route("{customerId}/keys")]
    public async Task<IActionResult> GetApiKeys(int customerId)
    {
        var dbCustomer = await mediator.Send(new GetCustomer(customerId));
        if (dbCustomer == null)
        {
            return this.HydraNotFound();
        }

        var urlRoots = GetUrlRoots();
        var collection = new HydraCollection<ApiKey>
        {
            WithContext = true,
            Members = dbCustomer.Keys.Select(
                    key => new ApiKey(urlRoots.BaseUrl, customerId, key, null))
                .ToArray(),
            TotalItems = dbCustomer.Keys.Length,
            PageSize = dbCustomer.Keys.Length,
            Id = Request.GetJsonLdId()
        };
        return Ok(collection);
    }
        
        
    /// <summary>
    /// POST /customers/id/keys
    ///
    /// Client can obtain a new key by posting an empty payload
    /// </summary>
    /// <param name="customerId"></param>
    /// <returns>newly created ApiKey</returns>
    [HttpPost]
    [Route("{customerId}/keys")]
    public async Task<IActionResult> CreateApiKey(int customerId)
    {
        var result = await mediator.Send(new CreateApiKey(customerId));
        if (result.Key.HasText() && result.Secret.HasText())
        {
            return Ok(new ApiKey(GetUrlRoots().BaseUrl, customerId, result.Key, result.Secret));
        }

        return this.HydraProblem("Unable to create API key", null, 500, "API Key");
    }
        
        
    /// <summary>
    /// DELETE /customers/id/keys/key
    ///
    /// Remove a key so that it can no longer be used.
    /// </summary>
    /// <param name="customerId"></param>
    /// <param name="key"></param>
    /// <returns>No content</returns>
    [HttpDelete]
    [Route("{customerId}/keys/{key}")]
    public async Task<IActionResult> DeleteApiKey(int customerId, string key)
    {
        var result = await mediator.Send(new DeleteApiKey(customerId, key));
        if (result.Error.HasText())
        {
            return this.HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request");
        }

        return NoContent();
    }
}