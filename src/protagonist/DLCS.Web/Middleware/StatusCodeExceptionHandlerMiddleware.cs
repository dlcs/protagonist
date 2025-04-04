﻿using System;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Exceptions;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Middleware;

/// <summary>
/// Middleware that uses <see cref="HttpException"/> to return formatted exception
/// </summary>
public class StatusCodeExceptionHandlerMiddleware
{
    private readonly RequestDelegate next;

    public StatusCodeExceptionHandlerMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (ArgumentException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
        catch (HttpException ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, HttpException exception)
        => new StatusCodeResponse(exception.StatusCode, exception.Message)
            .WriteJsonResponse(context.Response);
}