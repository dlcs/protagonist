using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Infrastructure.ReverseProxy;

public static class ReverseProxyHandling
{
    /// <summary>
    /// Log any errors that arise during reverse-proxy forwarding operations
    /// </summary>
    public static void HandleProxyError(this ForwarderError error, HttpContext httpContext,
        ForwarderRequestConfig requestOptions, ILogger logger)
    {
        if (error is ForwarderError.RequestCanceled or ForwarderError.RequestBodyCanceled
            or ForwarderError.ResponseBodyCanceled or ForwarderError.UpgradeRequestCanceled
            or ForwarderError.UpgradeResponseCanceled)
        {
            logger.LogDebug("Request cancelled for {Path}, error {Error}", httpContext.Request.Path, error);
            return;
        }

        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();

        if (errorFeature?.Exception == null)
        {
            logger.LogError("Proxy error {Error} for {Path} but IForwarderErrorFeature has empty exception", error,
                httpContext.Request.Path);
            return;
        }

        if (error is ForwarderError.RequestTimedOut)
        {
            logger.LogError("Proxy error {Error} for {Path}, ActivityTimeout: {ActivityTimeout}", error,
                httpContext.Request.Path, GetActivityTimeoutForLog(requestOptions));
            return;
        }

        logger.LogError(errorFeature.Exception, "Proxy error {Error} for {Path}", error, httpContext.Request.Path);
    }

    private static string GetActivityTimeoutForLog(ForwarderRequestConfig requestOptions) =>
        requestOptions.ActivityTimeout.HasValue
            ? $"{requestOptions.ActivityTimeout.Value.TotalMilliseconds}ms"
            : "default";
}