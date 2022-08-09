using System;
using System.Collections.Generic;
using API.Converters;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.Web.Requests;
using Hydra.Model;
using Microsoft.AspNetCore.Mvc;

namespace API;

/// <summary>
/// Base class for DLCS API Controllers that return Hydra responses
/// </summary>
public abstract class HydraController : Controller
{
    /// <summary>
    /// API Settings available to derived controller classes
    /// </summary>
    protected readonly ApiSettings Settings;
    
    /// <inheritdoc />
    protected HydraController(ApiSettings settings)
    {
        Settings = settings;
    }

    /// <summary>
    /// Used by derived controllers to construct correct fully qualified URLs in returned Hydra objects.
    /// </summary>
    /// <returns></returns>
    protected UrlRoots GetUrlRoots()
    {
        return new UrlRoots
        {
            BaseUrl = Request.GetBaseUrl(),
            ResourceRoot = Settings.DLCS.ResourceRoot.ToString()
        };
    }
    
    
    /// <summary>
    /// Evaluates incoming orderBy and orderByDescending fields to get a suitable
    /// ordering field and its direction.
    /// </summary>
    /// <param name="orderBy"></param>
    /// <param name="orderByDescending"></param>
    /// <param name="descending"></param>
    /// <returns></returns>
    protected string? GetOrderBy(string? orderBy, string? orderByDescending, out bool descending)
    {
        string? orderByField = null;
        descending = false;
        if (orderBy.HasText())
        {
            orderByField = orderBy;
        }
        else if (orderByDescending.HasText())
        {
            orderByField = orderByDescending;
            descending = true;
        }

        return orderByField;
    }

    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response.
    /// </summary>
    /// <param name="statusCode">The value for <see cref="Error.Status" />.</param>
    /// <param name="errorMessages">One or more string error messages.</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="title">The value for <see cref="Error.Title" />.</param>
    /// <param name="type">The value for <see cref="Error.Type" />.</param>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    [NonAction]
    protected virtual ObjectResult HydraProblem(
        IEnumerable<string>? errorMessages = null,
        string? instance = null,
        int? statusCode = null,
        string? title = null,
        string? type = null)
    {
        string? detail = null;
        if (errorMessages != null)
        {
            detail = string.Join("; ", errorMessages);
        }

        return HydraProblem(detail, instance, statusCode, title, type);
    }


    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response.
    /// </summary>
    /// <param name="statusCode">The value for <see cref="Error.Status" />.</param>
    /// <param name="detail">The value for <see cref="Error.Detail" />.</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="title">The value for <see cref="Error.Title" />.</param>
    /// <param name="type">The value for <see cref="Error.Type" />.</param>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    [NonAction]
    protected virtual ObjectResult HydraProblem(
        string? detail = null,
        string? instance = null,
        int? statusCode = null,
        string? title = null,
        string? type = null)
    {
        var hydraError = new Error
        {
            Detail = detail,
            Instance = instance ?? Request.GetDisplayUrl(),
            Status = statusCode ?? 500,
            Title = title,
            ErrorTypeUri = type,
        };

        return new ObjectResult(hydraError)
        {
            StatusCode = hydraError.Status
        };
    }
    
    
    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response.
    /// This overload can wrap otherwise uncaught exceptions.
    ///
    /// Usually a more specific Hydra Error response should be constructed.
    /// </summary>
    /// <param name="otherException"></param>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    [NonAction]
    protected virtual ObjectResult HydraProblem(Exception otherException)
    {
        return HydraProblem(otherException.Message, null, 500);
    }

    /// <summary> 
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public virtual ObjectResult HydraNotFound(string? detail = null)
    {
        return HydraProblem(detail, null, 404, "Not Found");
    }
}

