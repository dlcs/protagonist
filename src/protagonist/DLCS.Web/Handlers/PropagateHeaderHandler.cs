using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Web.Logging;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Handlers;

/// <summary>
/// A DelegatingHandler that propagates x-correlation-id to downstream services
/// </summary>
public class PropagateHeaderHandler(IHttpContextAccessor contextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Prefer the ambient value - it is still available for calls made from work that outlives the request that
        // started it, where IHttpContextAccessor.HttpContext has already been cleared.
        var headerValue = CorrelationIdContext.Current ??
                          contextAccessor.HttpContext.GetHeaderValueFromRequestOrResponse(
                              CorrelationIdContext.HeaderKey);
        if (headerValue.HasText()) AddCorrelationId(request, headerValue);

        return base.SendAsync(request, cancellationToken);
    }

    private static void AddCorrelationId(HttpRequestMessage request, string? correlationId)
    {
        request.Headers.TryAddWithoutValidation(CorrelationIdContext.HeaderKey, [correlationId]);
    }
}
