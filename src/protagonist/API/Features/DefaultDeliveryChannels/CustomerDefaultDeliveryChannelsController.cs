using API.Features.CustomHeaders.Converters;
using API.Features.CustomHeaders.Requests;
using API.Features.DefaultDeliveryChannels.Converters;
using API.Features.DefaultDeliveryChannels.Requests;
using API.Features.DefaultDeliveryChannels.Validation;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.DefaultDeliveryChannels;

/// <summary>
/// DLCS REST API Operations for Custom Headers
/// </summary>
[Route("/customers/{customerId}/defaultDeliveryChannels")]
[ApiController]
public class CustomerDefaultDeliveryChannelsController : HydraController
{
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
        var getCustomerDefaultDeliveryChannels = new GetCustomerDefaultDeliveryChannels(customerId);

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
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCustomerDefaultDeliveryChannel([FromRoute] int customerId, 
        [FromBody]DefaultDeliveryChannel defaultDeliveryChannel,
        [FromServices] HydraDefaultDeliveryChannelValidator validator,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerDefaultDeliveryChannel(customerId, defaultDeliveryChannel);

        try
        {
            var result = await Mediator.Send(command, cancellationToken);
            if (result.DefaultDeliveryChannel == null || result.ErrorMessages.Any())
            {
                int statusCode = result.Conflict ? 409 : 500;
                return this.HydraProblem(result.ErrorMessages, null, statusCode, "Could not create Default Delivery Channel");
            }
            var newApiCustomer = result.DefaultDeliveryChannel.ToHydra(GetUrlRoots().BaseUrl);
            return this.HydraCreated(newApiCustomer);
        }
        catch (Exception ex)
        {
            // Are exceptions the way this info should be passed back to the controller?
            return this.HydraProblem(ex);
        }
    }
    
    // /// <summary>
    // /// Create a single default delivery channel
    // /// </summary>
    // /// <returns>A Hydra JSON-LD default delivery channel object</returns>
    // [HttpPut("{defaultDeliveryChannelId}")]
    // [ProducesResponseType(StatusCodes.Status200OK)]
    // public async Task<IActionResult> UpdateCustomerDefaultDeliveryChannel([FromRoute] int customerId,
    //     [FromBody]DefaultDeliveryChannel defaultDeliveryChannel,
    //     [FromServices] HydraDefaultDeliveryChannelValidator validator,
    //     CancellationToken cancellationToken)
    // {
    //     var command = new UpdateCustomerDefaultDeliveryChannel(customerId, defaultDeliveryChannel);
    //     
    //     // var validationResult = await validator.ValidateAsync(customHeaderChanges, cancellationToken);
    //     // if (!validationResult.IsValid)
    //     // {
    //     //     return this.ValidationFailed(validationResult);
    //     // }
    //     
    //     command.Customer = customerId;
    //     
    //     
    //     
    //     customHeaderChanges.ModelId = customHeaderId;
    //     var request = new UpdateCustomHeader(customerId, customHeaderChanges.ToDlcsModel());
    //     
    //     return await HandleUpsert(request, 
    //         ch => ch.ToHydra(GetUrlRoots().BaseUrl),
    //         errorTitle: "Failed to update Custom Header",
    //         cancellationToken: cancellationToken);
    // }
}