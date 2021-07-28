using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration.Infrastructure
{
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

    public class ProxyResponse
    {
        public Uri Uri { get; set; }
        public HttpMethod Method { get; set;}

        public ProxyResponse()
        {
        }

        public ProxyResponse(HttpRequestMessage request)
        {
            Uri = request.RequestUri;
            Method = request.Method;
        }
    }
}