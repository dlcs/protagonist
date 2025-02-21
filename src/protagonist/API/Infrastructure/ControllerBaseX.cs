using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using FluentValidation.Results;
using Hydra;
using Hydra.Model;
using Microsoft.AspNetCore.Mvc;

namespace API.Infrastructure;

public static class ControllerBaseX
{
    /// <summary>
    /// Evaluates incoming orderBy and orderByDescending fields to get a suitable
    /// ordering field and its direction.
    /// </summary>
    public static string? GetOrderBy(this ControllerBase _, string? orderBy, string? orderByDescending,
        out bool descending)
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
    public static ObjectResult HydraProblem(
        this ControllerBase controller,
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

        return controller.HydraProblem(detail, instance, statusCode, title, type);
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
    public static ObjectResult HydraProblem(
        this ControllerBase controller,
        string? detail = null,
        string? instance = null,
        int? statusCode = null,
        string? title = null,
        string? type = null)
    {
        var hydraError = new Error
        {
            Detail = detail,
            Instance = instance ?? controller.Request.GetDisplayUrl(),
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
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult HydraProblem(this ControllerBase controller, Exception otherException)
    {
        return controller.HydraProblem(otherException.Message, null, 500);
    }

    /// <summary> 
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult HydraNotFound(this ControllerBase controller, string? detail = null)
    {
        return controller.HydraProblem(detail, null, 404, "Not Found");
    }

    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult ValidationFailed(this ControllerBase controller, ValidationResult validationResult)
    {
        var message = string.Join(". ", validationResult.Errors.Select(s => s.ErrorMessage).Distinct());
        return controller.HydraProblem(message, null, 400, "Bad request");
    }
    
    /// <summary>
    /// Creates an <see cref="ObjectResult"/> for specified hydraResource.
    /// This will be a 201 status code with Location header set to Id of resource.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static CreatedResult HydraCreated(this ControllerBase controller, DlcsResource hydraResource) 
        => controller.Created(hydraResource.Id, hydraResource);

    /// <summary>
    /// Create an IActionResult from specified ModifyEntityResult{T}.
    /// This will be the Hydra model + 200/201 on success. Or a Hydra
    /// error and appropriate status code if failed.
    /// </summary>
    /// <param name="controller">Current controllerBase object</param>
    /// <param name="entityResult">Result to transform</param>
    /// <param name="hydraBuilder">Delegate to transform ModifyEntityResult.Entity to Hydra representation</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="errorTitle">
    ///     The value for <see cref="Error.Title" />. In some instances this will be prepended to the actual error name.
    ///     e.g. errorTitle + ": Conflict"
    /// </param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <returns>
    /// ActionResult generated from ModifyEntityResult
    /// </returns>
    public static IActionResult ModifyResultToHttpResult<T>(this ControllerBase controller,
        ModifyEntityResult<T> entityResult,
        Func<T, DlcsResource> hydraBuilder, string? instance,
        string? errorTitle)
        where T : class =>
        entityResult.WriteResult switch
        {
            WriteResult.Updated => controller.Ok(hydraBuilder(entityResult.Entity)),
            WriteResult.Created => controller.HydraCreated(hydraBuilder(entityResult.Entity)),
            WriteResult.NotFound => controller.HydraNotFound(entityResult.Error),
            WriteResult.Error => controller.HydraProblem(entityResult.Error, instance, 500, errorTitle),
            WriteResult.BadRequest => controller.HydraProblem(entityResult.Error, instance, 400, errorTitle),
            WriteResult.Conflict => controller.HydraProblem(entityResult.Error, instance, 409, 
                $"{errorTitle}: Conflict"),
            WriteResult.FailedValidation => controller.HydraProblem(entityResult.Error, instance, 400,
                $"{errorTitle}: Validation failed"),
            WriteResult.StorageLimitExceeded => controller.HydraProblem(entityResult.Error, instance, 507,
                $"{errorTitle}: Storage limit exceeded"),
            _ => controller.HydraProblem(entityResult.Error, instance, 500, errorTitle),
        };

    /// <summary>
    /// Create an IActionResult from specified FetchEntityResult{T}.
    /// This will be the Hydra model + 200 on success. Or a Hydra
    /// error and appropriate status code if failed.
    /// </summary>
    /// <param name="controller">Current controllerBase object</param>
    /// <param name="entityResult">Result to transform</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="errorTitle">
    ///     The value for <see cref="Error.Title" />. In some instances this will be prepended to the actual error name.
    ///     e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="hydraBuilder">
    /// Optional delegate to transform Result.Entity to Hydra representation, if not provided Entity property returned
    /// as-is
    /// </param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <returns>
    /// ActionResult generated from FetchEntityResult
    /// </returns>
    public static IActionResult FetchResultToHttpResult<T>(this ControllerBase controller,
        FetchEntityResult<T> entityResult,
        string? instance,
        string? errorTitle,
        Func<T, JsonLdBase>? hydraBuilder = null)
        where T : class
    {
        if (entityResult.Error)
        {
            return controller.HydraProblem(entityResult.ErrorMessage, instance, 500, errorTitle);
        }
        
        if (entityResult.EntityNotFound || entityResult.Entity == null)
        {
            return controller.HydraNotFound();
        }

        return hydraBuilder == null
            ? controller.Ok(entityResult.Entity)
            : controller.Ok(hydraBuilder(entityResult.Entity));
    }
}