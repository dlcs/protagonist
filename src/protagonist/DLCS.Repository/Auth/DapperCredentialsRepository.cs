using System;
using System.IO;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Guard;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DLCS.Repository.Auth;

/// <summary>
/// Implementation of <see cref="ICredentialsRepository"/> using Dapper for data access
/// </summary>
public class DapperCredentialsRepository : ICredentialsRepository
{
    private readonly IBucketReader bucketReader;
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DapperCredentialsRepository> logger;
    private readonly JsonSerializer jsonSerializer;

    public DapperCredentialsRepository(IBucketReader bucketReader,
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ILogger<DapperCredentialsRepository> logger)
    {
        this.bucketReader = bucketReader;
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
        jsonSerializer = new JsonSerializer();
    }
    
    public Task<BasicCredentials?> GetBasicCredentialsForOriginStrategy(CustomerOriginStrategy customerOriginStrategy)
    {
        customerOriginStrategy.ThrowIfNull(nameof(customerOriginStrategy));
        var credentials =
            customerOriginStrategy.Credentials.ThrowIfNullOrWhiteSpace(nameof(customerOriginStrategy.Credentials));

        var cacheKey = $"OriginStrategy_Creds:{customerOriginStrategy.Id}";

        return appCache.GetOrAddAsync(cacheKey, () =>
        {
            logger.LogDebug("Refreshing CustomerOriginStrategy credentials for {CustomerOriginStrategy}",
                customerOriginStrategy.Id);
            return GetBasicCredentials(credentials, customerOriginStrategy.Id);
        }, cacheSettings.GetMemoryCacheOptions(priority: CacheItemPriority.Low));
    }
    
    private async Task<BasicCredentials?> GetBasicCredentials(string credentials, string id)
    {
        try
        {
            if (CredentialsAreForS3(credentials))
            {
                // get from s3
                return await GetBasicCredentialsFromBucket(credentials);
            }

            // deserialize from object in DB
            var basicCredentials = JsonConvert.DeserializeObject<BasicCredentials>(credentials);
            return basicCredentials;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting credentials for customerOriginStrategy {CustomerOriginStrategy}", id);
            throw;
        }
    }
    
    private static bool CredentialsAreForS3(string credentials) => credentials.StartsWith("s3://");

    private async Task<BasicCredentials?> GetBasicCredentialsFromBucket(string credentials)
    {
        var objectInBucket = RegionalisedObjectInBucket.Parse(credentials);
        var credentialStream = await bucketReader.GetObjectContentFromBucket(objectInBucket);
        
        if ((credentialStream ?? Stream.Null) == Stream.Null) return null;
        
        using var reader = new StreamReader(credentialStream!);
        using var jsonReader = new JsonTextReader(reader);
        return jsonSerializer.Deserialize<BasicCredentials>(jsonReader);
    }
}