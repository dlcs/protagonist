using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration.Infrastructure;

/// <summary>
/// Test HttpClientFactory that uses TestProxyHandler for handling. 
/// </summary>
public class TestProxyHttpClientFactory : IForwarderHttpClientFactory
{
    private readonly TestProxyHandler testProxyHandler;

    public TestProxyHttpClientFactory(TestProxyHandler testProxyHandler)
    {
        this.testProxyHandler = testProxyHandler;
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(testProxyHandler, false);
}

/// <summary>
/// ProxyHandler that echoes back any requests made.
/// </summary>
public class TestProxyHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request, ReasonPhrase = $"OK From {nameof(TestProxyHandler)}",
            Content = JsonContent.Create(new ProxyResponse(request))
        };
        return Task.FromResult<HttpResponseMessage>(response);
    }
}

public class TestProxyForwarder : IHttpForwarder
{
    public async ValueTask<ForwarderError> SendAsync(HttpContext context, string destinationPrefix, HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig, HttpTransformer transformer)
    {
        var destinationUri = new Uri(destinationPrefix);
        var requestUri = new UriBuilder(destinationUri.Scheme, destinationUri.Host, destinationUri.Port,
            context.Request.Path);
        
        // Create request and transform to outgoing
        var requestMessage = context.CreateProxyHttpRequest(requestUri.Uri);
        await transformer.TransformRequestAsync(context, requestMessage, destinationPrefix);
        
        // Create a fake 200 response from proxy + run through transformer
        var proxyResponse = new HttpResponseMessage(HttpStatusCode.OK);
        await context.CopyProxyHttpResponse(proxyResponse);
        await transformer.TransformResponseAsync(context, proxyResponse);

        // Write request parameters into response
        await context.Response.WriteAsJsonAsync(new ProxyResponse(requestMessage, requestConfig.ActivityTimeout));
        return ForwarderError.None;
    }
}

/// <summary>
/// Response written by <see cref="TestProxyHandler"/>, used to verify proxied requests
/// </summary>
public class ProxyResponse
{
    public Uri Uri { get; set; }
    public HttpMethod Method { get; set;}
    public TimeSpan? ActivityTimeout { get; set; }

    public ProxyResponse()
    {
    }

    public ProxyResponse(HttpRequestMessage request, TimeSpan? activityTimeout = null)
    {
        ActivityTimeout = activityTimeout;
        Uri = request.RequestUri;
        Method = request.Method;
    }
}