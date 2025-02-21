using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Web.Handlers;
using FakeItEasy;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Tests.Handlers;

public class PropagateHeaderHandlerTests
{
    private readonly DefaultHttpContext httpContext;
    private readonly PropagateHeaderHandler sut;

    public PropagateHeaderHandlerTests()
    {
        httpContext = new DefaultHttpContext();
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(httpContext);

        sut = new PropagateHeaderHandler(contextAccessor);
    }

    [Fact]
    public async Task SendAsync_HandlesNullHttpContext()
    {
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        var testHandler = new TestHandler();
        var handlerUnderTest = new PropagateHeaderHandler(A.Fake<IHttpContextAccessor>());
        handlerUnderTest.InnerHandler = testHandler;

        var invoker = new HttpMessageInvoker(handlerUnderTest);
        await invoker.SendAsync(httpRequestMessage, CancellationToken.None);

        testHandler.Request.Headers.Should().NotContainKey("x-correlation-id");
    }
    
    [Fact]
    public async Task SendAsync_SetsCorrelationId_FromRequest()
    {
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        var testHandler = new TestHandler();
        sut.InnerHandler = testHandler;

        httpContext.Request.Headers.Add("x-correlation-id", "from-request");
        var invoker = new HttpMessageInvoker(sut);
        await invoker.SendAsync(httpRequestMessage, CancellationToken.None);

        testHandler.Request.Headers.Should()
            .ContainSingle(x => x.Key == "x-correlation-id" && x.Value.Single() == "from-request");
    }
    
    [Fact]
    public async Task SendAsync_SetsCorrelationId_FromResponse()
    {
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        var testHandler = new TestHandler();
        sut.InnerHandler = testHandler;

        httpContext.Response.Headers.Add("x-correlation-id", "from-response");
        var invoker = new HttpMessageInvoker(sut);
        await invoker.SendAsync(httpRequestMessage, CancellationToken.None);

        testHandler.Request.Headers.Should()
            .ContainSingle(x => x.Key == "x-correlation-id" && x.Value.Single() == "from-response");
    }
    
    [Fact]
    public async Task SendAsync_MakesRequest_IfNoCorrelationId()
    {
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        var testHandler = new TestHandler();
        sut.InnerHandler = testHandler;

        var invoker = new HttpMessageInvoker(sut);
        await invoker.SendAsync(httpRequestMessage, CancellationToken.None);

        testHandler.Request.Headers.Should().NotContainKey("x-correlation-id");
    }
}

public class TestHandler : DelegatingHandler
{
    public HttpRequestMessage Request { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Request = request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}