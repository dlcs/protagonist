using System.Net;

namespace DLCS.Core;

public static class HttpStatusCodeX
{
    /// <summary>
    /// Get a value checking if status code is 2xx
    /// </summary>
    /// <param name="statusCode">StatusCode to check</param>
    /// <returns>True if 2xx, else False</returns>
    /// <remarks>JQuery has 304 as a success status code</remarks>
    public static bool IsSuccess(this HttpStatusCode statusCode)
        => (int)statusCode is >= 200 and < 300;

    /// <summary>
    /// Get a value checking if status code is 5xx
    /// </summary>
    /// <param name="statusCode">StatusCode to check</param>
    /// <returns>True if 5xx, else False</returns>
    public static bool IsServerError(this HttpStatusCode statusCode)
        => (int)statusCode >= 500;
}