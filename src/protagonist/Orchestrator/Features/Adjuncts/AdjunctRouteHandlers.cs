using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Streams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.IdRewriter;
using Orchestrator.Infrastructure.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Features.Adjuncts;

public static class AdjunctRouteHandlers
{
    internal const string RoutePrefix = "adjuncts";

    private static readonly HttpMessageInvoker HttpClient;
    private static readonly ForwarderRequestConfig RequestOptions;

    static AdjunctRouteHandlers()
    {
        HttpClient = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip,
            UseCookies = false
        });
        
        RequestOptions = new ForwarderRequestConfig {ActivityTimeout = TimeSpan.FromSeconds(60)};
    }
    
    /// <summary>
    /// Add endpoint mappings for /adjuncts/ paths
    /// </summary>
    /// <param name="endpoints">Current <see cref="IEndpointRouteBuilder"/> object.</param>
    public static void MapAdjunctHandling(this IEndpointRouteBuilder endpoints)
    {
        var requestHandler = endpoints.GetRequiredService<AdjunctRequestHandler>();
        var forwarder = endpoints.GetRequiredService<IHttpForwarder>();
        var bucketReader = endpoints.GetRequiredService<IBucketReader>();
        var logger = endpoints.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AdjunctRouteHandlers));
        endpoints.Map("/adjuncts/{customer}/{space}/{assetId}/{adjunctId}", async httpContext =>
        {
            logger.LogDebug("Handling request '{Path}'", httpContext.Request.Path);
            var proxyResponse = await requestHandler.HandleRequest(httpContext);
            await ProcessResponse(logger, httpContext, forwarder, bucketReader, proxyResponse);
        });
    }

    private static async Task ProcessResponse(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
        IBucketReader bucketReader, IProxyActionResult proxyActionResult)
    {
        if (proxyActionResult is StatusCodeResult statusCodeResult)
        {
            httpContext.Response.StatusCode = (int) statusCodeResult.StatusCode;
            foreach (var header in statusCodeResult.Headers)
            {
                httpContext.Response.Headers.Add(header);
            }
            return;
        }

        if (proxyActionResult is IdRewriteProxyActionResult rewriteResult)
        {
            await RewriteAndStreamAdjunct(logger, httpContext, bucketReader, rewriteResult);
            return;
        }

        var proxyAction = proxyActionResult as ProxyActionResult;
        await ProxyRequest(logger, httpContext, forwarder, proxyAction);
    }

    private static async Task RewriteAndStreamAdjunct(ILogger logger, HttpContext httpContext,
        IBucketReader bucketReader, IdRewriteProxyActionResult rewriteResult)
    {
        try
        {
            var objectFromBucket =
                await bucketReader.GetObjectFromBucket(rewriteResult.Source, httpContext.RequestAborted);

            if (objectFromBucket.Stream.IsNull())
            {
                logger.LogWarning("Annotation adjunct not found in S3 for {Path}", httpContext.Request.Path);
                httpContext.Response.StatusCode = (int) HttpStatusCode.NotFound;
                return;
            }

            var contentLength = objectFromBucket.Headers.ContentLength;
            logger.LogDebug("Rewriting annotation adjunct id for {Path}, size {ContentLength} bytes",
                httpContext.Request.Path, contentLength);

            if (rewriteResult.MaxSizeBytes.HasValue && contentLength > rewriteResult.MaxSizeBytes)
            {
                logger.LogWarning(
                    "Annotation adjunct at {Path} exceeds max permitted size ({ContentLength} > {MaxSize} bytes)",
                    httpContext.Request.Path, contentLength, rewriteResult.MaxSizeBytes);
                httpContext.Response.StatusCode = 500;
                return;
            }

            await using var s3Stream = objectFromBucket.Stream;
            using var bufferedInput = new MemoryStream((int?)contentLength ?? 0);
            await s3Stream.CopyToAsync(bufferedInput, httpContext.RequestAborted);
            bufferedInput.Seek(0, SeekOrigin.Begin);

            using var outputStream = new MemoryStream((int)bufferedInput.Length);
            StreamingJsonProcessor.ProcessJson(
                bufferedInput,
                outputStream,
                bufferedInput.Length,
                new TopLevelIdRewriteProcessor(rewriteResult.NewId));

            foreach (var header in rewriteResult.Headers)
            {
                httpContext.Response.Headers[header.Key] = header.Value;
            }

            outputStream.Seek(0, SeekOrigin.Begin);
            await outputStream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rewriting annotation adjunct id for {Path}", httpContext.Request.Path);
            httpContext.Response.StatusCode = 500;
        }
    }

    private static async Task ProxyRequest(ILogger logger, HttpContext httpContext, IHttpForwarder forwarder,
        ProxyActionResult proxyAction)
    {
        // Note: copied from FileRouteHandlers
        
        // TODO - what do we do if it's not in S3?
        // We need a 'custom' handler that will not invoke Yarp and stream instead
        if (proxyAction.Target != ProxyDestination.S3)
        {
            logger.LogError("Found unexpected proxyTarget '{TargetCluster}' - only S3 supported",
                proxyAction.Target);
            httpContext.Response.StatusCode = 502;
            return;
        }
        
        var transformer = new PathRewriteTransformer(proxyAction, true);

        var error = await forwarder.SendAsync(httpContext, proxyAction.Path!, HttpClient, RequestOptions,
            transformer);

        if (error != ForwarderError.None)
        {
            error.HandleProxyError(httpContext, RequestOptions, logger);
        }
    }
}
