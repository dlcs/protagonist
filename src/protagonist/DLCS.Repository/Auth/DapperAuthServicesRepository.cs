using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Auth;

public class DapperAuthServicesRepository : IDapperConfigRepository, IAuthServicesRepository
{
    public IConfiguration Configuration { get; }
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DapperAuthServicesRepository> logger;

    public DapperAuthServicesRepository(
        IConfiguration configuration, 
        IAppCache appCache, 
        IOptions<CacheSettings> cacheOptions,
        ILogger<DapperAuthServicesRepository> logger)
    {
        this.Configuration = configuration;
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
    }
    
    public async Task<IEnumerable<AuthService>> GetAuthServicesForRole(int customer, string role)
    {
        var cacheKey = $"authsvc:{customer}:{role}";

        return await appCache.GetOrAddAsync(cacheKey, async () =>
        {
            logger.LogDebug("Refreshing {CacheKey} from database", cacheKey);
            return await GetAuthServicesFromDatabase(customer, role);
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
    }

    public async Task<AuthService?> GetAuthServiceByName(int customer, string name)
    {
        var cacheKey = $"authsvc:{customer}:name:{name}";

        try
        {
            return await appCache.GetOrAddAsync(cacheKey, async () =>
            {
                logger.LogDebug("Refreshing {CacheKey} from database", cacheKey);
                return await this.QuerySingleOrDefaultAsync<AuthService>(
                    AuthServiceByNameSql, new {Customer = customer, Name = name});
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
        }
        catch (InvalidOperationException e)
        {
            logger.LogError(e, "Unable to find authservice with name {Name} for customer {Customer}", name,
                customer);
            return null;
        }
    }

    public async Task<Role?> GetRole(int customer, string role)
    {
        var cacheKey = $"role:{customer}:{role}";

        try
        {
            return await appCache.GetOrAddAsync(cacheKey, async () =>
            {
                logger.LogDebug("Refreshing {CacheKey} from database", cacheKey);
                return await this.QuerySingleOrDefaultAsync<Role>(RoleByIdSql, new { Customer = customer, Role = role });
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
        }
        catch (InvalidOperationException e)
        {
            logger.LogError(e, "Unable to find role with id {Role} for customer {Customer}", role, customer);
            return null;
        }
    }

    public async Task<RoleProvider?> GetRoleProvider(string roleProviderId)
    {
        var cacheKey = $"rp:{roleProviderId}";

        try
        {
            return await appCache.GetOrAddAsync(cacheKey, async () =>
            {
                logger.LogDebug("Refreshing {CacheKey} from database", cacheKey);
                return await this.QuerySingleOrDefaultAsync<RoleProvider>(
                    RoleProviderByIdSql, new { Id = roleProviderId });
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short, priority: CacheItemPriority.Low));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to find roleprovider with id {RoleProviderId}", roleProviderId);
            return null;
        }
    }

    private async Task<IEnumerable<AuthService>> GetAuthServicesFromDatabase(int customer, string role)
    {
        var result = await this.QueryAsync<AuthService>(AuthServiceSql,
            new { Customer = customer, Role = role });

        var authServices = result.ToList();
        if (authServices.IsNullOrEmpty())
        {
            logger.LogInformation("Found no authServices for customer {Customer}, role {Role}", customer, role);
            return Enumerable.Empty<AuthService>();
        }
        
        // All services have a token service so add to collection
        authServices.Add(new AuthService
        {
            Customer = customer,
            Name = "token",
            Profile = Constants.ProfileV1.Token
        });

        return authServices;
    }

    public Role CreateRole(string name, int customer, string authServiceId)
    {
        return new()
        {
            Id = GetRoleIdFromName(name, customer),
            Customer = customer,
            Name = name,
            AuthService = authServiceId,
            Aliases = string.Empty
        };
    }

    public AuthService CreateAuthService(int customerId, string profile, string name, int ttl)
    {            
        return new AuthService
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Profile = profile,
            Name = name,
            Ttl = ttl,
            CallToAction = string.Empty,
            ChildAuthService = string.Empty,
            Description = string.Empty,
            Label = string.Empty,
            PageDescription = string.Empty,
            PageLabel = string.Empty,
            RoleProvider = string.Empty
        };
    }

    private string GetRoleIdFromName(string name, int customer)
    {
        // This is a namespace for roles, not necessarily the current URL
        const string fqRolePrefix = "https://api.dlcs.io";  
        string firstCharLowered = name.Trim()[0].ToString().ToLowerInvariant() + name.Substring(1);
        return $"{fqRolePrefix}/customers/{customer}/roles/{firstCharLowered.ToCamelCase()}";
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

    private const string AuthServiceByNameSql = @"
SELECT ""Id"", ""Customer"", ""Name"", ""Profile"", ""Label"", ""Description"", ""PageLabel"", ""PageDescription"", ""CallToAction"", ""TTL"", ""RoleProvider"", ""ChildAuthService""
FROM ""AuthServices"" c
WHERE ""Name"" = @Name AND ""Customer"" = @Customer
";

    private const string RoleByIdSql = @"
SELECT ""Id"", ""Customer"", ""AuthService"", ""Name"", ""Aliases"" FROM ""Roles"" WHERE ""Customer"" = @Customer AND ""Id"" = @Role
";

    private const string RoleProviderByIdSql = @"
SELECT ""Id"", ""Customer"", ""AuthService"", ""Configuration"", ""Credentials"" from ""RoleProviders"" WHERE ""Id"" = @Id 
";
}