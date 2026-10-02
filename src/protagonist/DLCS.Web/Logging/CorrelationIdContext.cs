using System;
using System.Threading;
using Serilog.Context;

namespace DLCS.Web.Logging;

/// <summary>
/// Ambient correlation-id for the current async flow.
/// </summary>
/// <remarks>
/// This deliberately does not use <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>. That is backed by an
/// AsyncLocal holder object which ASP.NET Core mutates on request teardown, so every ExecutionContext captured during
/// the request sees a null HttpContext once the request has finished. A plain AsyncLocal is captured by value, so work
/// that outlives the request that started it (async continuations, shared cache factory delegates) keeps the id.
/// </remarks>
public static class CorrelationIdContext
{
    /// <summary>
    /// HttpHeader that carries the correlation-id between services.
    /// </summary>
    public const string HeaderKey = "x-correlation-id";

    /// <summary>
    /// Name of the Serilog property that carries the correlation-id.
    /// </summary>
    public const string PropertyName = "CorrelationId";

    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    /// <summary>
    /// Correlation-id for the current async flow, or null if one has not been established.
    /// </summary>
    public static string? Current => CurrentCorrelationId.Value;

    /// <summary>
    /// Establish correlation-id for the current async flow and add it to the Serilog LogContext.
    /// </summary>
    /// <param name="correlationId">Unique identifier for the unit of work.</param>
    /// <returns>
    /// Disposable that pops the property from the LogContext. Note that the ambient value is left in place - anything
    /// that captured this ExecutionContext keeps the id it was started with.
    /// </returns>
    public static IDisposable Set(string correlationId)
    {
        CurrentCorrelationId.Value = correlationId;
        return LogContext.PushProperty(PropertyName, correlationId, false);
    }
}
