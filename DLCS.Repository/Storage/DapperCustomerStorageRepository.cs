using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Storage;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Storage
{
    public class DapperCustomerStorageRepository : DapperRepository, IStorageRepository
    {
        private readonly CacheSettings cacheSettings;
        private readonly IAppCache appCache;
        private readonly ILogger<DapperCustomerStorageRepository> logger;
        private static readonly StoragePolicy NullStoragePolicy = new() { Id = "__nullstoragepolicy__" };
        
        public DapperCustomerStorageRepository(
            IConfiguration configuration, 
            IAppCache appCache,
            IOptions<CacheSettings> cacheOptions,
            ILogger<DapperCustomerStorageRepository> logger) : base(configuration)
        {
            this.appCache = appCache;
            this.logger = logger;
            cacheSettings = cacheOptions.Value;
        }

        public async Task<StoragePolicy?> GetStoragePolicy(string id, CancellationToken cancellationToken)
        {
            var key = $"storagePolicy:{id}";
            var storagePolicy = await appCache.GetOrAddAsync(key, async entry =>
            {
                var dbPolicy = await QueryFirstOrDefaultAsync<StoragePolicy>(StoragePolicySql, new { Id = id });
                if (dbPolicy == null)
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                    return NullStoragePolicy;
                }

                return dbPolicy;
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short));

            return storagePolicy.Id == NullStoragePolicy.Id ? null : storagePolicy;
        }
        
        private const string StoragePolicySql = @"SELECT ""Id"", ""MaximumNumberOfStoredImages"", ""MaximumTotalSizeOfStoredImages"" 
FROM public.""StoragePolicies""  WHERE ""Id""=@Id;";

        public async Task<CustomerStorage?> GetCustomerStorage(int customerId, int spaceId, bool createOnDemand, CancellationToken cancellationToken)
        {
            // TODO: this is wrong, this is not how the customer storage works
            string sql = CustomerStorageSqlBase + @" AND ""Space""=@Space";
            var customerStorage = await QueryFirstOrDefaultAsync<CustomerStorage>(sql, new { Customer = customerId, Space = spaceId });
            if (customerStorage == null && createOnDemand)
            {
                // TODO: create this record
                // customerStorage = ...
            }

            return customerStorage;
        }
        
        private const string CustomerStorageSqlBase = @"SELECT ""Customer"", ""StoragePolicy"", 
""NumberOfStoredImages"", ""TotalSizeOfStoredImages"", ""TotalSizeOfThumbnails"", ""LastCalculated"", ""Space"" 
FROM public.""CustomerStorage""  WHERE ""Customer""=@Customer;";
        
        
        public async Task<CustomerStorageSummary> GetCustomerStorageSummary(int customerId)
        {
            // Is it quicker to do ths in the database? Depends how many spaces the customer has.
            
            // TODO: this logic is not correct!
            var spaceStorageList =
                await QueryAsync<CustomerStorage>(CustomerStorageSqlBase, new { Customer = customerId });
            var summary = new CustomerStorageSummary
            {
                CustomerId = customerId
            };
            foreach (var customerStorage in spaceStorageList)
            {
                summary.NumberOfStoredImages += customerStorage.NumberOfStoredImages;
                summary.TotalSizeOfStoredImages += customerStorage.TotalSizeOfStoredImages;
                summary.TotalSizeOfThumbnails += customerStorage.TotalSizeOfThumbnails;
            }

            return summary;
        }

        public async Task<ImageCountStorageMetric> GetImageCounts(int putAssetCustomer)
        {
            // TODO: this is fake for now
            return await Task.FromResult(new ImageCountStorageMetric
            {
                CurrentNumberOfStoredImages = 1, MaximumNumberOfStoredImages = 1000
            });
        }
    }
}