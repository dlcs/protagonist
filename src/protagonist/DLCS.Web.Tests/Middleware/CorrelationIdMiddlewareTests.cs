using System;
using System.Threading.Tasks;
using DLCS.Web.Logging;
using DLCS.Web.Middleware;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_UsesCorrelationIdFromRequestHeader_IfPresent()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(CorrelationIdContext.HeaderKey, "from-request");
        string ambient = null;
        var sut = new CorrelationIdMiddleware(_ =>
        {
            ambient = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        // Act
        await sut.InvokeAsync(context);

        // Assert
        ambient.Should().Be("from-request");
        context.Request.Headers[CorrelationIdContext.HeaderKey].ToString().Should().Be("from-request");
        context.Response.Headers[CorrelationIdContext.HeaderKey].ToString().Should().Be("from-request");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvokeAsync_GeneratesCorrelationId_IfRequestHeaderPresentButEmpty(string headerValue)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(CorrelationIdContext.HeaderKey, headerValue);
        string ambient = null;
        var sut = new CorrelationIdMiddleware(_ =>
        {
            ambient = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        // Act
        await sut.InvokeAsync(context);

        // Assert
        ambient.Should().NotBeNullOrEmpty();
        Guid.TryParse(ambient, out _).Should().BeTrue("generated correlation-id is a guid");
        context.Request.Headers[CorrelationIdContext.HeaderKey].ToString().Should().Be(ambient);
        context.Response.Headers[CorrelationIdContext.HeaderKey].ToString().Should().Be(ambient);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_IfNoRequestHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        string ambient = null;
        var sut = new CorrelationIdMiddleware(_ =>
        {
            ambient = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        // Act
        await sut.InvokeAsync(context);

        // Assert
        ambient.Should().NotBeNullOrEmpty();
        Guid.TryParse(ambient, out _).Should().BeTrue("generated correlation-id is a guid");
        context.Request.Headers[CorrelationIdContext.HeaderKey].ToString().Should().Be(ambient);
        context.Response.Headers[CorrelationIdContext.HeaderKey].ToString().Should().Be(ambient);
    }

    [Fact]
    public async Task InvokeAsync_SetsSingleHeaderValue_IfRequestHeaderHasMultipleValues()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(CorrelationIdContext.HeaderKey, new[] { "first", "second" });
        var sut = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await sut.InvokeAsync(context);

        // Assert
        context.Request.Headers[CorrelationIdContext.HeaderKey].Should().ContainSingle().Which.Should().Be("first");
        context.Response.Headers[CorrelationIdContext.HeaderKey].Should().ContainSingle().Which.Should().Be("first");
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdSurvives_WorkThatOutlivesRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(CorrelationIdContext.HeaderKey, "outlives-request");
        var tcs = new TaskCompletionSource();
        Task<string> continuation = null;
        var sut = new CorrelationIdMiddleware(_ =>
        {
            // captures current ExecutionContext but completes after the middleware has returned
            continuation = tcs.Task.ContinueWith(_ => CorrelationIdContext.Current);
            return Task.CompletedTask;
        });

        // Act
        await sut.InvokeAsync(context);
        tcs.SetResult();

        // Assert
        (await continuation).Should().Be("outlives-request");
    }
}
