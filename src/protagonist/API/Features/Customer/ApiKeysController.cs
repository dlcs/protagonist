using System.Net;
using API.Features.Customer.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Customer;

/// <summary>
/// DLCS REST API Operations for API Keys.
/// </summary>
[Route("/customers/")]
[ApiController]
public class ApiKeysController : HydraController
{
    public ApiKeysController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get a list of all the API Keys available for this customer
    /// </summary>
    /// <param name="customerId">Customer Id to get keys for</param>
    /// <returns>HydraCollection of ApiKey objects</returns>
    [HttpGet]
    [Route("{customerId}/keys")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<ApiKey>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetApiKeys(int customerId)
    {
        var dbCustomer = await Mediator.Send(new GetCustomer(customerId));
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
    /// Obtain a new API key by posting an empty payload.
    ///
    /// The return value contains both Key and Secret - it is the only time the Secret is visible
    /// </summary>
    /// <param name="customerId">Customer Id to create key for</param>
    /// <returns>Newly created ApiKey</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/keys
    ///     { }
    /// </remarks>
    [HttpPost]
    [Route("{customerId}/keys")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiKey))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(Error))]
    public async Task<IActionResult> CreateApiKey(int customerId)
    {
        var result = await Mediator.Send(new CreateApiKey(customerId));
        if (result.CreateSuccess)
        {
            return Ok(new ApiKey(GetUrlRoots().BaseUrl, customerId, result.Key, result.Secret));
        }

        return this.HydraProblem("Unable to create API key", null, 500, "API Key");
    }

    /// <summary>
    /// Remove a key so that it can no longer be used.
    /// </summary>
    /// <param name="customerId">Customer Id that owns key to be deleted</param>
    /// <param name="key">Key to remove</param>
    /// <returns>No content</returns>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Error))]
    [HttpDelete]
    [Route("{customerId}/keys/{key}")]
    public async Task<IActionResult> DeleteApiKey(int customerId, string key)
    {
        var result = await Mediator.Send(new DeleteApiKey(customerId, key));
        if (result.Error.HasText())
        {
            return this.HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request");
        }

        return NoContent();
    }
}