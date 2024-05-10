using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Handlers;

/// <summary>
/// A DelegatingHandler that propagates x-correlation-id to downstream services
/// </summary>
public class PropagateHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor contextAccessor;
    private const string CorrelationHeaderKey = "x-correlation-id";
    
    public PropagateHeaderHandler(IHttpContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor;
    }
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (contextAccessor.HttpContext == null) return base.SendAsync(request, cancellationToken);

        var headerValue = contextAccessor.HttpContext.GetHeaderValueFromRequestOrResponse(CorrelationHeaderKey);
        if (!string.IsNullOrEmpty(headerValue))
        {
            AddCorrelationId(request, headerValue);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void AddCorrelationId(HttpRequestMessage request, string? correlationId)
    {
        request.Headers.TryAddWithoutValidation(CorrelationHeaderKey, new[] { correlationId });
    }
}