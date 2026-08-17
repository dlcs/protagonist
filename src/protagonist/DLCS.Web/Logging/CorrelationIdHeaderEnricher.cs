using System;
using DLCS.Core.Guard;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace DLCS.Web.Logging;

/// <summary>
/// Serilog event enricher that adds "CorrelationId" property, preferring the ambient
/// <see cref="CorrelationIdContext"/> and falling back to reading it from httpHeader. If neither is present it is
/// generated and added to the headers.
/// </summary>
/// <remarks>
/// The httpHeader fallback is based on https://github.com/ekmsystems/serilog-enrichers-correlation-id but optionally
/// sets correlation-id on current HttpRequest in addition to HttpResponse. This makes it easier to handle in things
/// like YARP.
///
/// Prefer registering <see cref="DLCS.Web.Middleware.CorrelationIdMiddleware"/> - the fallback path here depends on
/// IHttpContextAccessor, which ASP.NET Core clears on request teardown, so log events raised from work that outlives
/// the request would otherwise not be correlated.
/// </remarks>
public class CorrelationIdHeaderEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor contextAccessor;
    private readonly string headerKey;
    private readonly bool addToRequest;

    public CorrelationIdHeaderEnricher(string headerKey, bool addToRequest)
        : this(headerKey, addToRequest, new HttpContextAccessor())
    {
    }

    internal CorrelationIdHeaderEnricher(string headerKey, bool addToRequest, IHttpContextAccessor contextAccessor)
    {
        this.headerKey = headerKey;
        this.contextAccessor = contextAccessor;
        this.addToRequest = addToRequest;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationIdContext.Current ?? GetCorrelationIdFromHeaders();
        if (string.IsNullOrEmpty(correlationId)) return;

        var correlationIdProperty =
            new LogEventProperty(CorrelationIdContext.PropertyName, new ScalarValue(correlationId));

        logEvent.AddOrUpdateProperty(correlationIdProperty);
    }

    private string? GetCorrelationIdFromHeaders()
    {
        var httpContext = contextAccessor.HttpContext;
        if (httpContext == null) return null;

        var header = httpContext.GetHeaderValueFromRequestOrResponse(headerKey);

        var correlationId = string.IsNullOrEmpty(header)
            ? Guid.NewGuid().ToString()
            : header;

        // Serilog swallows enricher exceptions to SelfLog, which would silently drop the property. Setting the headers
        // is best-effort - it can race with the response starting - but the property itself must always be added.
        try
        {
            if (addToRequest && !httpContext.Request.Headers.ContainsKey(headerKey))
            {
                httpContext.Request.Headers.Append(headerKey, correlationId);
            }

            if (!httpContext.Response.HasStarted && !httpContext.Response.Headers.ContainsKey(headerKey))
            {
                httpContext.Response.Headers.Append(headerKey, correlationId);
            }
        }
        catch (Exception)
        {
            // no-op, we still have a correlationId to log
        }

        return correlationId;
    }
}

public static class CorrelationIdLoggerConfigurationExtensions
{
    /// <summary>
    /// Add CorrelationId property to Serilog context, sourced from <see cref="CorrelationIdContext"/> if set, else from
    /// HttpHeader. If neither is present then it will be generated and added to the HttpHeaders.
    /// </summary>
    /// <param name="enrichmentConfiguration">Current <see cref="LoggerEnrichmentConfiguration"/> instance</param>
    /// <param name="headerKey">HttpHeader where CorrelationId is found</param>
    /// <param name="addToRequest">
    /// If true CorrelationId is added to current HttpRequest if it is missing. If false it is added to response only.
    /// Only relevant to the HttpHeader fallback.
    /// </param>
    /// <returns><see cref="LoggerConfiguration"/> object</returns>
    public static LoggerConfiguration WithCorrelationIdHeader(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        string headerKey = CorrelationIdContext.HeaderKey, bool addToRequest = false)
    {
        enrichmentConfiguration.ThrowIfNull(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With(new CorrelationIdHeaderEnricher(headerKey, addToRequest));
    }
}
