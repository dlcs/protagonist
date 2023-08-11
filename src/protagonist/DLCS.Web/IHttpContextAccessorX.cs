using DLCS.Core.Guard;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web;

public static class IHttpContextAccessorX
{
    /// <summary>
    /// Safely access the IHttpContextAccessor.HttpContext property, throwing an exception if null
    /// </summary>
    public static HttpContext SafeHttpContext(this IHttpContextAccessor httpContextAccessor)
        => httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext));
}