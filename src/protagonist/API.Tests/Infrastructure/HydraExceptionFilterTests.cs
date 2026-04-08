using System;
using System.Collections.Generic;
using API.Exceptions;
using API.Infrastructure;
using FakeItEasy;
using Hydra.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace API.Tests.Infrastructure;

public class HydraExceptionFilterTests
{
    private readonly ILogger<HydraExceptionFilter> logger = A.Fake<ILogger<HydraExceptionFilter>>();

    private HydraExceptionFilter GetSut() => new(logger);

    private static ExceptionContext BuildContext(Exception exception)
    {
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Scheme = "https",
                Host = new HostString("test.example"),
                Path = "/test"
            }
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
    }

    [Fact]
    public async Task OnExceptionAsync_SetsExceptionHandled()
    {
        var context = BuildContext(new Exception("oops"));
        await GetSut().OnExceptionAsync(context);
        context.ExceptionHandled.Should().BeTrue();
    }

    [Fact]
    public async Task OnExceptionAsync_GenericException_Returns500()
    {
        var context = BuildContext(new Exception("something went wrong"));
        await GetSut().OnExceptionAsync(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(500);
        var error = result.Value.Should().BeOfType<Error>().Subject;
        error.Status.Should().Be(500);
        error.Detail.Should().Be("something went wrong");
        error.Title.Should().Be("Unexpected error");
    }

    [Fact]
    public async Task OnExceptionAsync_ApiException_UsesStatusCodeAndLabel()
    {
        var ex = new APIException("custom message") { StatusCode = 422, Label = "Unprocessable" };
        var context = BuildContext(ex);
        await GetSut().OnExceptionAsync(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(422);
        var error = result.Value.Should().BeOfType<Error>().Subject;
        error.Status.Should().Be(422);
        error.Detail.Should().Be("custom message");
        error.Title.Should().Be("Unprocessable");
    }

    [Fact]
    public async Task OnExceptionAsync_BadRequestException_Returns400()
    {
        var context = BuildContext(new BadRequestException("bad input"));
        await GetSut().OnExceptionAsync(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(400);
        var error = result.Value.Should().BeOfType<Error>().Subject;
        error.Status.Should().Be(400);
        error.Detail.Should().Be("bad input");
        error.Title.Should().Be("Bad Request");
    }

    [Fact]
    public async Task OnExceptionAsync_ApiException_WithNoStatusCode_Returns500()
    {
        var ex = new APIException("no status set");
        var context = BuildContext(ex);
        await GetSut().OnExceptionAsync(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task OnExceptionAsync_SetsInstanceFromRequestUrl()
    {
        var context = BuildContext(new BadRequestException("err"));
        await GetSut().OnExceptionAsync(context);

        var error = ((ObjectResult)context.Result!).Value.Should().BeOfType<Error>().Subject;
        error.Instance.Should().Be("https://test.example");
    }
}
