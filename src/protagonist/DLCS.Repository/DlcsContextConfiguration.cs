using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository;

/// <summary>
/// Helpers for configuring db context
/// </summary>
public static class DlcsContextConfiguration
{
    private static string ConnectionStringKey = "PostgreSQLConnection";
    private static string RunMigrationsKey = "RunMigrations";

    /// <summary>
    /// Register and configure <see cref="DlcsContext"/> 
    /// </summary>
    public static IServiceCollection AddDlcsContext(this IServiceCollection services,
        IConfiguration configuration)
        => services
            .AddDbContext<DlcsContext>(options => SetupOptions(configuration, options));
    
    /// <summary>
    /// Run EF migrations if "RunMigrations" = true
    /// </summary>
    public static void TryRunMigrations(IConfiguration configuration, ILogger logger)
    {
        if (configuration.GetValue(RunMigrationsKey, false))
        {
            using var context = new DlcsContext(GetOptionsBuilder(configuration).Options);
            
            var pendingMigrations = context.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Count == 0)
            {
                logger.LogInformation("No migrations to run");
                return;
            }

            logger.LogInformation("Running migrations: {Migrations}", string.Join(",", pendingMigrations));
            context.Database.Migrate();
        }
    }

    /// <summary>
    /// Get a new instantiated <see cref="DlcsContext"/> object
    /// </summary>
    public static DlcsContext GetNewDbContext(IConfiguration configuration)
        => new(GetOptionsBuilder(configuration).Options);

    private static DbContextOptionsBuilder<DlcsContext> GetOptionsBuilder(IConfiguration configuration)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DlcsContext>();
        SetupOptions(configuration, optionsBuilder);
        return optionsBuilder;
    }

    private static void SetupOptions(IConfiguration configuration,
        DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(configuration.GetConnectionString(ConnectionStringKey), builder => builder.SetPostgresVersion(13, 0));

}