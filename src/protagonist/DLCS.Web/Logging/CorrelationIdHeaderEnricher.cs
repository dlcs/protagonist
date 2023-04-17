using System;
using DLCS.Core.Guard;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace DLCS.Web.Logging;

/// <summary>
/// Serilog event enricher that adds "CorrelationId" property from httpHeader. If header not found it is added
/// </summary>
/// <remarks>
/// This is based on https://github.com/ekmsystems/serilog-enrichers-correlation-id but optionally sets correlation-id
/// on current HttpRequest in addition to HttpResponse. This makes it easier to handle in things like YARP
/// </remarks>
public class CorrelationIdHeaderEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor contextAccessor;
    private readonly string headerKey;
    private readonly bool addToRequest;
    private const string CorrelationIdPropertyName = "CorrelationId";
    
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
        if (contextAccessor.HttpContext == null) return;

        var correlationId = GetCorrelationId();

        var correlationIdProperty = new LogEventProperty(CorrelationIdPropertyName, new ScalarValue(correlationId));

        logEvent.AddOrUpdateProperty(correlationIdProperty);
    }

    private string GetCorrelationId()
    {
        var header = contextAccessor.HttpContext.GetHeaderValueFromRequestOrResponse(headerKey);
        
        var correlationId = string.IsNullOrEmpty(header)
            ? Guid.NewGuid().ToString()
            : header;

        // If HttpContext is null we'd never get here
        var httpContext = contextAccessor.HttpContext!;
        if (addToRequest)
        {
            if (!httpContext.Request.Headers.ContainsKey(headerKey))
            {
                httpContext.Request.Headers.Add(headerKey, correlationId);
            }
        }
        
        if (!httpContext.Response.HasStarted && !httpContext.Response.Headers.ContainsKey(headerKey))
        {
            httpContext.Response.Headers.Add(headerKey, correlationId);
        }

        return correlationId;
    }
}

public static class CorrelationIdLoggerConfigurationExtensions
{
    /// <summary>
    /// Add CorrelationId property to Serilog context, sourced from HttpHeader. If HttpHeader not present then it
    /// will be added.
    /// </summary>
    /// <param name="enrichmentConfiguration">Current <see cref="LoggerEnrichmentConfiguration"/> instance</param>
    /// <param name="headerKey">HttpHeader where CorrelationId is found</param>
    /// <param name="addToRequest">
    /// If true CorrelationId is added to current HttpRequest if it is missing. If false it is added to response only.
    /// </param>
    /// <returns><see cref="LoggerConfiguration"/> object</returns>
    public static LoggerConfiguration WithCorrelationIdHeader(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        string headerKey = "x-correlation-id", bool addToRequest = false)
    {
        enrichmentConfiguration.ThrowIfNull(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With(new CorrelationIdHeaderEnricher(headerKey, addToRequest));
    }
}