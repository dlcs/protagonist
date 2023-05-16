﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Helper utilities for dealing with Asset requests
/// </summary>
public class AssetRequestProcessor
{
    private readonly ILogger<AssetRequestProcessor> logger;
    private readonly IAssetTracker assetTracker;
    private readonly IAssetDeliveryPathParser assetDeliveryPathParser;

    public AssetRequestProcessor(
        ILogger<AssetRequestProcessor> logger,
        IAssetTracker assetTracker,
        IAssetDeliveryPathParser assetDeliveryPathParser)
    {
        this.logger = logger;
        this.assetTracker = assetTracker;
        this.assetDeliveryPathParser = assetDeliveryPathParser;
    }

    /// <summary>
    /// Try and parse current asset request, handling possible errors that may occur
    /// </summary>
    /// <param name="httpContext"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>Parsed asset request, if successful. Else error status code.</returns>
    public async Task<(T? assetRequest, HttpStatusCode? statusCode)> TryGetAssetDeliveryRequest<T>(
        HttpContext httpContext) where T : BaseAssetRequest, new()
    {
        try
        {
            var assetRequest =
                await assetDeliveryPathParser.Parse<T>(httpContext.Request.Path);
            return (assetRequest, null);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogError(ex, "Could not find Customer/Space from '{Path}'", httpContext.Request.Path);
            return (null, HttpStatusCode.NotFound);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Error parsing path '{Path}'", httpContext.Request.Path);
            return (null, HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            // TODO - is this the correct status?
            logger.LogError(ex, "Error parsing path '{Path}'", httpContext.Request.Path);
            return (null, HttpStatusCode.BadRequest);
        }
    }

    public async Task<OrchestrationAsset?> GetAsset(BaseAssetRequest assetRequest)
    {
        var imageId = assetRequest.GetAssetId();
        var asset = await assetTracker.GetOrchestrationAsset(imageId);
        return asset;
    }
}