#nullable enable
using System;
using System.Net;

namespace DLCS.Core.Exceptions;

/// <summary>
/// Exception thrown as the result of an Http call.
/// </summary>
public class HttpException : Exception
{
    /// <summary>
    /// StatusCode associated with exception.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    public HttpException(HttpStatusCode statusCode, string? message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpException(HttpStatusCode statusCode, string? message, Exception? inner)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}