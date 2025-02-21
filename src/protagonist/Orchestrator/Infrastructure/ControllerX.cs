using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Extension methods for 
/// </summary>
public static class ControllerX
{
    /// <summary>
    /// Handle asset requests, setting status code for known exceptions
    /// </summary>
    /// <returns>Result of controllerAction</returns>
    public static async Task<IActionResult> HandleAssetRequest(this ControllerBase controller,
        Func<Task<IActionResult>> controllerAction, ILogger? logger = null)
    {
        try
        {
            return await controllerAction();
        }
        catch (KeyNotFoundException ex)
        {
            logger?.LogError(ex, "Could not find Customer/Space from '{Path}'", controller.Request.Path);
            return controller.NotFound();
        }
        catch (FormatException ex)
        {
            logger?.LogError(ex, "Error parsing path '{Path}'", controller.Request.Path);
            return controller.BadRequest();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unknown exception handling projection '{Path}'", controller.Request.Path);
            return controller.StatusCode(500);
        }
    }

    /// <summary>
    /// Set cache headers (public, private, maxAge + sharedMaxAge) 
    /// </summary>
    public static void SetCacheControl(this ControllerBase controller, bool requiresAuth, TimeSpan maxAge)
    {
        controller.Response.GetTypedHeaders().CacheControl =
            new CacheControlHeaderValue
            {
                Public = !requiresAuth,
                Private = requiresAuth,
                MaxAge = maxAge,
                SharedMaxAge = requiresAuth ? null : maxAge
            };
    }
    
    /// <summary>
    /// Get a 303 result with "Location" header set
    /// </summary>
    public static StatusCodeResult SeeAlsoResult(this ControllerBase controller, string location)
    {
        controller.Response.Headers["Location"] = location;
        return new StatusCodeResult(303);
    }

    /// <summary>
    /// Get a 202 result with "Retry-After" header set
    /// </summary>
    public static IActionResult InProcess(this ControllerBase controller, int retryAfter)
    {
        controller.Response.Headers.Add("Retry-After", retryAfter.ToString());
        return new StatusCodeResult(202);
    }
}