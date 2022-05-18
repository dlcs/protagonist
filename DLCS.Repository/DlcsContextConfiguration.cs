using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository
{
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
                .AddDbContext<DlcsContext>(options =>
                    options.UseNpgsql(configuration.GetConnectionString(ConnectionStringKey)));
        
        /// <summary>
        /// Run EF migrations if "RunMigrations" = true
        /// </summary>
        public static void TryRunMigrations(IConfiguration configuration, ILogger logger)
        {
            if (configuration.GetValue(RunMigrationsKey, false))
            {
                var connection = configuration.GetConnectionString(ConnectionStringKey);
                using var context = new DlcsContext(
                    new DbContextOptionsBuilder<DlcsContext>()
                        .UseNpgsql(connection)
                        .Options);
                
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
    }
}