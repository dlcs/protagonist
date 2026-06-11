using Hydra.Model;
using Microsoft.AspNetCore.Mvc;

namespace API.Infrastructure;

internal static class HydraResponseBuilder
{
    /// <summary>
    /// Creates an <see cref="ObjectResult"/> containing a <see cref="Error"/> with the given values.
    /// </summary>
    /// <remarks>
    /// This isn't doing a lot on it's own but is used by both controller extension methods and
    /// <see cref="HydraExceptionFilter"/>.
    /// </remarks>
    public static ObjectResult CreateHydraErrorResult(string? detail, string? instance, int statusCode,
        string? title = null, string? type = null)
    {
        var error = new Error
        {
            Detail = detail,
            Instance = instance,
            Status = statusCode,
            Title = title,
            ErrorTypeUri = type,
        };
        return new ObjectResult(error) { StatusCode = statusCode };
    }
}
