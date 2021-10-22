using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.Customers;
using DLCS.Model.PathElements;
using DLCS.Model.Security;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Assets.CustomHeaders;
using DLCS.Repository.Customers;
using DLCS.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orchestrator.Infrastructure
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Add all dataaccess dependencies 
        /// </summary>
        public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
            => services
                .AddSingleton<ICustomerRepository, DapperCustomerRepository>()
                .AddSingleton<IPathCustomerRepository, CustomerPathElementRepository>()
                .AddSingleton<IAssetRepository, DapperAssetRepository>()
                .AddSingleton<IThumbRepository, ThumbRepository>()
                .AddSingleton<IThumbnailPolicyRepository, ThumbnailPolicyRepository>()
                .AddSingleton<ICredentialsRepository, DapperCredentialsRepository>()
                .AddSingleton<IAuthServicesRepository, DapperAuthServicesRepository>()
                .AddSingleton<ICustomHeaderRepository, DapperCustomHeaderRepository>()
                .AddScoped<ICustomerOriginStrategyRepository, CustomerOriginStrategyRepository>()
                .AddDbContext<DlcsContext>(opts =>
                    opts.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection"))
                );
    }
}