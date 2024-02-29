using API.Features.DeliveryChannels.Converters;
using API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;
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
/// DLCS REST API Operations for Default Delivery Channels
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
        var getCustomerDefaultDeliveryChannels = new GetDefaultDeliveryChannels(customerId, DefaultSpace);

        return await HandlePagedFetch<DLCS.Model.DeliveryChannels.DefaultDeliveryChannel, GetDefaultDeliveryChannels,
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
        Guid defaultDeliveryChannelId,
        CancellationToken cancellationToken)
    {
        var getCustomerDefaultDeliveryChannel = new GetDefaultDeliveryChannel(defaultDeliveryChannelId);

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
        [FromBody] DefaultDeliveryChannel defaultDeliveryChannel,
        [FromServices] HydraDefaultDeliveryChannelValidator validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(defaultDeliveryChannel, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        try
        {
            var command = new CreateDefaultDeliveryChannel(customerId,
                DefaultSpace,
                defaultDeliveryChannel.Policy,
                defaultDeliveryChannel.Channel,
                defaultDeliveryChannel.MediaType);
            
            return await HandleUpsert(command,
                s => s.DefaultDeliveryChannel.ToHydra(GetUrlRoots().BaseUrl),
                errorTitle: "Failed to create Default Delivery Channel",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return BadRequest();
        }
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
        Guid defaultDeliveryChannelId,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(defaultDeliveryChannel, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        var command = new UpdateDefaultDeliveryChannel(customerId, 
            DefaultSpace, 
            defaultDeliveryChannel.Policy,
            defaultDeliveryChannel.Channel,
            defaultDeliveryChannel.MediaType, 
            defaultDeliveryChannelId);

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
        Guid defaultDeliveryChannelId,
        CancellationToken cancellationToken)
    {
        var deleteCustomerDefaultDeliveryChannel = new DeleteDefaultDeliveryChannel(customerId, defaultDeliveryChannelId);
    
        return await HandleDelete(
            deleteCustomerDefaultDeliveryChannel,
            errorTitle: "Get default delivery channel failed",
            cancellationToken: cancellationToken
        );
    }
}