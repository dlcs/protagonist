using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DLCS.AWS.SSM;

public static class ConfigurationBuilderX
{
    /// <summary>
    /// Add AWS SystemsManager (SSM) as a configuration source if Production hosting environment.
    /// By default prefix is /protagonist/ but this can be overriden via SSM_PREFIX envvar 
    /// </summary>
    public static IConfigurationBuilder AddSystemsManager(this IConfigurationBuilder builder,
        HostBuilderContext builderContext)
    {
        if (!builderContext.HostingEnvironment.IsProduction()) return builder;

        var path = Environment.GetEnvironmentVariable("SSM_PREFIX") ?? "protagonist";
        return builder.AddSystemsManager(configureSource =>
        {
            configureSource.Path = $"/{path}/";
            configureSource.ReloadAfter = TimeSpan.FromMinutes(90);
        });
    }
}