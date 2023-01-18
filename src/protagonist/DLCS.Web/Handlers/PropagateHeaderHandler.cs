using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DLCS.Web.Handlers;

/// <summary>
/// A DelegatingHandler that propagates x-correlation-id to downstream services
/// </summary>
public class PropagateHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor contextAccessor;
    private static readonly string CorrelationHeaderKey = "x-correlation-id";
    
    public PropagateHeaderHandler(IHttpContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor;
    }
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (contextAccessor.HttpContext == null) return base.SendAsync(request, cancellationToken);
        
        // NOTE, we check both the Request and Response.
        // The Serilog .WithCorrelationIdHeader() set id on response if not provided in request
        if (TryGetCorrelationId(contextAccessor.HttpContext.Request.Headers, out var fromRequest))
        {
            AddCorrelationId(request, fromRequest);
        }
        else if (TryGetCorrelationId(contextAccessor.HttpContext.Response.Headers, out var fromResponse))
        {
            AddCorrelationId(request, fromResponse);
        }
        
        return base.SendAsync(request, cancellationToken);
    }

    private static void AddCorrelationId(HttpRequestMessage request, string? correlationId)
    {
        request.Headers.TryAddWithoutValidation(CorrelationHeaderKey, new[] { correlationId });
    }

    private bool TryGetCorrelationId(IHeaderDictionary headers, out string? correlationId)
    {
        correlationId = null;
        if (headers.TryGetValue(CorrelationHeaderKey, out var values))
        {
            correlationId = values.FirstOrDefault();
            return !StringValues.IsNullOrEmpty(correlationId);
        }

        return false;
    }
}