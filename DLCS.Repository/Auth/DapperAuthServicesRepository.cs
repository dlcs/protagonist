using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Core.Collections;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Auth
{
    public class DapperAuthServicesRepository : IAuthServicesRepository
    {
        private readonly IConfiguration configuration;
        private readonly IAppCache appCache;
        private readonly CacheSettings cacheSettings;
        private readonly ILogger<DapperAuthServicesRepository> logger;

        public DapperAuthServicesRepository(IConfiguration configuration, 
            IAppCache appCache, 
            IOptions<CacheSettings> cacheOptions,
            ILogger<DapperAuthServicesRepository> logger)
        {
            this.configuration = configuration;
            this.appCache = appCache;
            this.logger = logger;
            cacheSettings = cacheOptions.Value;
        }
        
        public async Task<IEnumerable<AuthService>> GetAuthServicesForRole(int customer, string role)
        {
            var cacheKey = $"authsvc:{customer}:{role}";

            return await appCache.GetOrAddAsync(cacheKey, async () =>
            {
                logger.LogDebug("refreshing {CacheKey} from database", cacheKey);
                return await GetAuthServicesFromDatabase(customer, role);
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
        }

        public async Task<Role?> GetRole(int customer, string role)
        {
            var cacheKey = $"role:{customer}:{role}";

            try
            {
                return await appCache.GetOrAddAsync(cacheKey, async () =>
                {
                    logger.LogDebug("refreshing {CacheKey} from database", cacheKey);
                    await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
                    return await connection.QuerySingleOrDefaultAsync<Role>(RoleByIdSql, new { Customer = customer, Role = role });
                }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
            }
            catch (InvalidOperationException e)
            {
                logger.LogError(e, "Unable to find role with id {Role} for customer {Customer}", role, customer);
                return null;
            }
        }

        private async Task<IEnumerable<AuthService>> GetAuthServicesFromDatabase(int customer, string role)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            var result = await connection.QueryAsync<AuthService>(AuthServiceSql,
                new { Customer = customer, Role = role });

            var authServices = result.ToList();
            if (authServices.IsNullOrEmpty())
            {
                logger.LogInformation("Found no authServices for customer {Customer}, role {Role}", customer, role);
                return Enumerable.Empty<AuthService>();
            }

            return authServices;
        }

        private const string AuthServiceSql = @"
WITH RECURSIVE cte_auth AS (
    SELECT p.""Id"", p.""Customer"", p.""Name"", p.""Profile"", p.""Label"", p.""Description"", p.""PageLabel"", p.""PageDescription"", p.""CallToAction"", p.""TTL"", p.""RoleProvider"", p.""ChildAuthService""
    FROM ""AuthServices"" p
    INNER JOIN ""Roles"" r on p.""Id"" = r.""AuthService""
    WHERE r.""Customer"" = @Customer AND r.""Id"" = @Role
    UNION ALL
    SELECT c.""Id"", c.""Customer"", c.""Name"", c.""Profile"", c.""Label"", c.""Description"", c.""PageLabel"", c.""PageDescription"", c.""CallToAction"", c.""TTL"", c.""RoleProvider"", c.""ChildAuthService""
    FROM ""AuthServices"" c
    INNER JOIN cte_auth ON c.""Id"" = cte_auth.""ChildAuthService""
)
SELECT ""Id"", ""Customer"", ""Name"", ""Profile"", ""Label"", ""Description"", ""PageLabel"", ""PageDescription"", ""CallToAction"", ""TTL"", ""RoleProvider"", ""ChildAuthService""
FROM cte_auth;
";

        private const string RoleByIdSql = @"
SELECT ""Id"", ""Customer"", ""AuthService"", ""Name"", ""Aliases"" FROM ""Roles"" WHERE ""Customer"" = @Customer AND ""Id"" = @Role
";
    }
}