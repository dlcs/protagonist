﻿using API.Features.Storage.Converters;
using API.Features.Storage.Requests;
using API.Infrastructure;
using API.Settings;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Storage;

/// <summary>
/// DLCS REST API Operations for customer storage objects
/// </summary>
public class StorageController : HydraController
{
    public StorageController(
        IMediator mediator,
        IOptions<ApiSettings> settings) : base(settings.Value, mediator)
    {
    }

    /// <summary>
    /// Gets the storage object of an image within a customer's space
    /// </summary>
    [HttpGet]
    [Route("/customers/{customerId}/spaces/{spaceId}/images/{imageId}/storage")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.ImageStorage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetImageStorage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        [FromRoute] string imageId,
        CancellationToken cancellationToken)
    {
        var request = new GetImageStorage(customerId, spaceId, imageId);
        
        return await HandleFetch(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get storage for image",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Get the storage object of a customer's space
    /// </summary>
    [HttpGet]
    [Route("/customers/{customerId}/spaces/{spaceId}/storage")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.CustomerStorage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetSpaceStorage(
        [FromRoute] int customerId,
        [FromRoute] int spaceId,
        CancellationToken cancellationToken)
    {
        var request = new GetSpaceStorage(customerId, spaceId);
        
        return await HandleFetch(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get storage for space",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Get the customer's default storage object
    /// </summary>
    [HttpGet]
    [Route("/customers/{customerId}/storage")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DLCS.HydraModel.CustomerStorage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetCustomerStorage(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var request = new GetCustomerStorage(customerId);
        
        return await HandleFetch(request, 
            s => s.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get storage",
            cancellationToken: cancellationToken);
    }
}