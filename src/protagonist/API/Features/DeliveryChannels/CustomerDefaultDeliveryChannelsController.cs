using API.Features.DeliveryChannels.Converters;
using API.Features.DeliveryChannels.Requests;
using API.Features.DeliveryChannels.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

/// <summary>
/// DLCS REST API Operations for Custom Headers
/// </summary>
[Route("/customers/{customerId}/defaultDeliveryChannels")]
[ApiController]
public class CustomerDefaultDeliveryChannelsController : HydraController
{
    private const int DefaultSpace = 0;
    
    public CustomerDefaultDeliveryChannelsController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get paged list of all customer accessible default delivery channels (customer specific + system)
    ///
    /// Supports ?page= and ?pageSize= query parameters
    /// </summary>
    /// <returns>Collection of Hydra JSON-LD default delivery channel objects</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerDefaultDeliveryChannels([FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var getCustomerDefaultDeliveryChannels = new GetCustomerDefaultDeliveryChannels(customerId, DefaultSpace);

        return await HandlePagedFetch<DLCS.Model.DeliveryChannels.DefaultDeliveryChannel, GetCustomerDefaultDeliveryChannels,
            DefaultDeliveryChannel>(
            getCustomerDefaultDeliveryChannels,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get default delivery channels failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get an individual customer accessible default delivery channel (customer specific + system)
    /// </summary>
    /// <returns>A Hydra JSON-LD default delivery channel object</returns>
    [HttpGet("{defaultDeliveryChannelId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCustomerDefaultDeliveryChannel(
        string defaultDeliveryChannelId,
        CancellationToken cancellationToken)
    {
        var getCustomerDefaultDeliveryChannel = new GetCustomerDefaultDeliveryChannel(defaultDeliveryChannelId);

        return await HandleFetch(
            getCustomerDefaultDeliveryChannel,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get default delivery channel failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Create a single default delivery channel
    /// </summary>
    /// <returns>A Hydra JSON-LD default delivery channel object</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCustomerDefaultDeliveryChannel([FromRoute] int customerId,
        [FromBody]DefaultDeliveryChannel defaultDeliveryChannel,
        [FromServices] HydraDefaultDeliveryChannelValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(defaultDeliveryChannel, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        var command = new CreateCustomerDefaultDeliveryChannel(customerId,  DefaultSpace, defaultDeliveryChannel);
        
        return await HandleUpsert(command, 
            s => s.DefaultDeliveryChannel.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create origin strategy",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Update a default delivery channel
    /// </summary>
    /// <returns>A Hydra JSON-LD default delivery channel object</returns>
    [HttpPut("{defaultDeliveryChannelId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCustomerDefaultDeliveryChannel([FromRoute] int customerId,
        [FromBody]DefaultDeliveryChannel defaultDeliveryChannel,
        [FromServices] HydraDefaultDeliveryChannelValidator validator,
        string defaultDeliveryChannelId,
        CancellationToken cancellationToken)
    {

        var validationResult = await validator.ValidateAsync(defaultDeliveryChannel, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        defaultDeliveryChannel.Id = defaultDeliveryChannelId;
        
        var command = new UpdateCustomerDefaultDeliveryChannel(customerId, DefaultSpace, defaultDeliveryChannel);

        return await HandleUpsert(command, 
            ch => ch.DefaultDeliveryChannel.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update Default Delivery Channel",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Get an individual customer accessible default delivery channel (customer specific + system)
    /// </summary>
    /// <returns>A Hydra JSON-LD default delivery channel object</returns>
    [HttpDelete("{defaultDeliveryChannelId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomerDefaultDeliveryChannel([FromRoute] int customerId,
        string defaultDeliveryChannelId,
        CancellationToken cancellationToken)
    {
        var deleteCustomerDefaultDeliveryChannel = new DeleteCustomerDefaultDeliveryChannel(customerId, defaultDeliveryChannelId);
    
        return await HandleDelete(
            deleteCustomerDefaultDeliveryChannel,
            errorTitle: "Get default delivery channel failed",
            cancellationToken: cancellationToken
        );
    }
}