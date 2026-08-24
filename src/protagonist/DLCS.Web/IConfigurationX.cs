using DLCS.Core.Guard;
using Microsoft.Extensions.Configuration;

namespace DLCS.Web;

public static class IConfigurationX
{
    /// <summary>
    /// Calls configuration.Get{T}(), throwing if null
    /// </summary>
    public static T GetRequired<T>(this IConfiguration configuration)
        => configuration.Get<T>().ThrowIfNull(typeof(T).Name);
}
