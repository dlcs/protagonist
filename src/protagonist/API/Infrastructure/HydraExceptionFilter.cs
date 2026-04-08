using API.Exceptions;
using DLCS.Web.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace API.Infrastructure;

/// <summary>
/// Global exception filter that catches <see cref="APIException"/> any unhandled exceptions, converting them to Hydra
/// <see cref="Hydra.Model.Error"/> responses.
/// </summary>
public class HydraExceptionFilter(ILogger<HydraExceptionFilter> logger) : IAsyncExceptionFilter
{
    private const int InternalServerErrorStatusCode = 500;
    private const string UnhandledExceptionTitle = "Unexpected error";

    public Task OnExceptionAsync(ExceptionContext context)
    {
        var instance = context.HttpContext.Request.GetDisplayUrl();

        context.Result = context.Exception is APIException apiException
            ? CreateApiExceptionResult(apiException, instance)
            : CreateUnhandledExceptionResult(context.Exception, instance);

        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }

    private ObjectResult CreateApiExceptionResult(APIException exception, string instance)
    {
        logger.LogError(exception, "APIException handling request {Url}", instance);
        return HydraResponseBuilder.CreateHydraErrorResult(
            exception.Message,
            instance,
            exception.StatusCode ?? InternalServerErrorStatusCode,
            exception.Label);
    }

    private ObjectResult CreateUnhandledExceptionResult(Exception exception, string instance)
    {
        logger.LogError(exception, "Unhandled exception handling request {Url}", instance);
        return HydraResponseBuilder.CreateHydraErrorResult(
            exception.Message,
            instance,
            InternalServerErrorStatusCode,
            UnhandledExceptionTitle);
    }
}
