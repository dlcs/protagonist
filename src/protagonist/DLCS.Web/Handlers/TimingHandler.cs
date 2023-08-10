using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DLCS.Web.Handlers;

/// <summary>
/// A basic delegating handler that logs Trace level timing notifications
/// </summary>
public class TimingHandler : DelegatingHandler
{
    private readonly ILogger<TimingHandler> logger;

    public TimingHandler(ILogger<TimingHandler> logger)
    {
        this.logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        logger.LogTrace("Calling {HttpMethod} {Uri}..", request.Method, request.RequestUri);
        
        var result = await base.SendAsync(request, cancellationToken);
        
        sw.Stop();
        logger.LogTrace("Request to {HttpMethod} {Uri} completed with status {StatusCode} in {Elapsed}ms",
            request.Method, request.RequestUri, result.StatusCode, sw.ElapsedMilliseconds);
        return result;
    }
}