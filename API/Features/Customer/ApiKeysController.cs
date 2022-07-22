using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Features.Customer.Requests;
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
    private readonly IMediator mediator;

    /// <inheritdoc />
    public ApiKeysController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value)
    {
        this.mediator = mediator;
    }
    
    
    // ################# GET /customers/id/keys #####################
    [HttpGet]
    [Route("{customerId}/keys")]
    public async Task<IActionResult> GetApiKeys(int customerId)
    {
        var dbCustomer = await mediator.Send(new GetCustomer(customerId));
        if (dbCustomer == null)
        {
            return HydraNotFound();
        }

        var urlRoots = getUrlRoots();
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
        
        
    // ################# POST /customers/id/keys #####################
    [HttpPost]
    [Route("{customerId}/keys")]
    public async Task<IActionResult> CreateNewApiKey(int customerId)
    {
        var result = await mediator.Send(new CreateApiKey(customerId));
        if (result.Key.HasText() && result.Secret.HasText())
        {
            return Ok(new ApiKey(getUrlRoots().BaseUrl, customerId, result.Key, result.Secret));
        }

        return HydraProblem("Unable to create API key", null, 500, "API Key", null);
    }
        
        
    // ################# DELETE /customers/id/keys/key #####################
    [HttpDelete]
    [Route("{customerId}/keys/{key}")]
    public async Task<IActionResult> DeleteKey(int customerId, string key)
    {
        var result = await mediator.Send(new DeleteApiKey(customerId, key));
        if (result.Error.HasText())
        {
            return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request", null);
        }

        return NoContent();
    }


}