using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Storage;

public class CustomerStorageRepository : IStorageRepository
{
    private readonly DlcsContext dlcsContext;
    private readonly IPolicyRepository policyRepository;
    private readonly ILogger<CustomerStorageRepository> logger;

    public CustomerStorageRepository(
        DlcsContext dlcsContext,
        IPolicyRepository policyRepository,
        ILogger<CustomerStorageRepository> logger)
    {
        this.dlcsContext = dlcsContext;
        this.policyRepository = policyRepository;
        this.logger = logger;
    }

    public async Task<CustomerStorageSummary> GetCustomerStorageSummary(
        int customerId, CancellationToken cancellationToken)
    {
        var aggregateRow = await dlcsContext.CustomerStorages
            .AsNoTracking()
            .SingleOrDefaultAsync(cs => cs.Customer == customerId && cs.Space == null, cancellationToken);

        return aggregateRow == null
            ? new CustomerStorageSummary { CustomerId = customerId }
            : new CustomerStorageSummary
            {
                CustomerId = customerId,
                NumberOfStoredImages = aggregateRow.NumberOfStoredImages,
                TotalSizeOfStoredImages = aggregateRow.TotalSizeOfStoredImages,
                TotalSizeOfThumbnails = aggregateRow.TotalSizeOfThumbnails,
                NumberOfStoredAdjuncts = aggregateRow.NumberOfStoredAdjuncts,
                TotalSizeOfStoredAdjuncts = aggregateRow.TotalSizeOfStoredAdjuncts,
            };
    }

    public async Task<AssetStorageMetric> GetStorageMetrics(int customerId, CancellationToken cancellationToken)
    {
        var aggregateRecord = await dlcsContext.CustomerStorages
            .SingleOrDefaultAsync(cs => cs.Customer == customerId && cs.Space == null, cancellationToken);

        // The aggregate row is seeded by CreateCustomer and backfilled by migration - its absence is a bug.
        aggregateRecord.ThrowIfNull(nameof(aggregateRecord));

        var policyName = string.IsNullOrEmpty(aggregateRecord!.StoragePolicy)
            ? StoragePolicy.DefaultStoragePolicyName
            : aggregateRecord.StoragePolicy;

        var policy = await policyRepository.GetStoragePolicy(policyName, cancellationToken);
        return new AssetStorageMetric
        {
            Policy = policy.ThrowIfNull(nameof(policy))!,
            CustomerStorage = aggregateRecord,
        };
    }
}