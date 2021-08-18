using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Core.Collections;
using DLCS.Model.Security;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Security
{
    public class DapperAuthServicesRepository : IAuthServicesRepository
    {
        private readonly IConfiguration configuration;
        private readonly IAppCache appCache;
        private readonly ILogger<DapperCredentialsRepository> logger;

        public DapperAuthServicesRepository(IConfiguration configuration, 
            IAppCache appCache, 
            ILogger<DapperCredentialsRepository> logger)
        {
            this.configuration = configuration;
            this.appCache = appCache;
            this.logger = logger;
        }
        
        public async Task<IEnumerable<AuthService>> GetAuthServiceForRole(int customer, string role)
        {
            var cacheKey = $"roles:{customer}:{role}";

            return await appCache.GetOrAddAsync(cacheKey, async entry =>
            {
                logger.LogDebug("refreshing {CacheKey} from database", cacheKey);
                return await GetAuthServicesFromDatabase(customer, role, entry);
            });
        }

        private async Task<IEnumerable<AuthService>> GetAuthServicesFromDatabase(int customer, string role, ICacheEntry entry)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            var result = await connection.QueryAsync<AuthService>(AuthServiceSql,
                new { Customer = customer, Role = role });

            var authServices = result.ToList();
            if (authServices.IsNullOrEmpty())
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); // TODO - config
                return Enumerable.Empty<AuthService>();
            }

            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // TODO - config
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
    }
}