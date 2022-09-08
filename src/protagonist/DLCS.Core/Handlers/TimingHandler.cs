using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DLCS.Core.Handlers;

/// <summary>
/// A basic delegating handler that logs Debug level timing notifications
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
        logger.LogDebug("Calling {Uri}..", request.RequestUri);
        var result = await base.SendAsync(request, cancellationToken);
        sw.Stop();
        logger.LogDebug("Request to {Uri} completed with status {StatusCode} in {Elapsed}ms", request.RequestUri,
            result.StatusCode, sw.ElapsedMilliseconds);
        return result;
    }
}