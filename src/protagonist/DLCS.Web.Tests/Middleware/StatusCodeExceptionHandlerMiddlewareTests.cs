using System.IO;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Exceptions;
using DLCS.Web.Middleware;
using DLCS.Web.Response;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Xunit;

namespace DLCS.Web.Tests.Middleware;

public class StatusCodeExceptionHandlerMiddlewareTests
{
    [Theory]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task InvokeAsync_SetsResponseStatusCodeAndBody_IfInternalErrorThrown(HttpStatusCode statusCode)
    {
        // Arrange
        const string message = "Feed me a stray cat";
        var sut = new StatusCodeExceptionHandlerMiddleware(ctx =>
            throw new HttpException(statusCode, message));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await sut.InvokeAsync(context);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var streamText = reader.ReadToEnd();
        var objResponse = JsonConvert.DeserializeObject<StatusCodeResponse>(streamText);
        
        // Assert
        context.Response.StatusCode.Should().Be((int)statusCode);
        objResponse.Should().BeEquivalentTo(new StatusCodeResponse(statusCode, message));
    }
}