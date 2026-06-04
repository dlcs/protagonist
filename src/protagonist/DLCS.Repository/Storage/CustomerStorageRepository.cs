using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Core.Types;
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

        if (aggregateRow == null)
        {
            logger.LogWarning("No aggregate CustomerStorage row found for customer {CustomerId}", customerId);
            return new CustomerStorageSummary { CustomerId = customerId };
        }

        return new CustomerStorageSummary
        {
            CustomerId = customerId,
            NumberOfStoredImages = aggregateRow.NumberOfStoredImages,
            TotalSizeOfStoredImages = aggregateRow.TotalSizeOfStoredImages,
            TotalSizeOfThumbnails = aggregateRow.TotalSizeOfThumbnails,
            NumberOfStoredAdjuncts = aggregateRow.NumberOfStoredAdjuncts,
            TotalSizeOfStoredAdjuncts = aggregateRow.TotalSizeOfStoredAdjuncts,
        };
    }

    public Task DecrementAdjunctStorage(AssetId assetId, long adjunctSize, CancellationToken cancellationToken) =>
        DecrementAdjunctStorage(assetId, adjunctSize, 1, cancellationToken);

    public async Task DecrementAdjunctStorage(AssetId assetId, long adjunctSize, int adjunctCount,
        CancellationToken cancellationToken)
    {
        await dlcsContext.CustomerStorages
            .Where(cs => cs.Customer == assetId.Customer && (cs.Space == null || cs.Space == assetId.Space))
            .UpdateFromQueryAsync(cs => new CustomerStorage
            {
                NumberOfStoredAdjuncts = cs.NumberOfStoredAdjuncts > adjunctCount ? cs.NumberOfStoredAdjuncts - adjunctCount : 0,
                TotalSizeOfStoredAdjuncts = cs.TotalSizeOfStoredAdjuncts > adjunctSize
                    ? cs.TotalSizeOfStoredAdjuncts - adjunctSize
                    : 0
            }, cancellationToken);

        await dlcsContext.ImageStorages.DecrementAdjunctSize(assetId, adjunctSize, cancellationToken);
    }

    public async Task<AssetStorageMetric> GetStorageMetrics(int customerId, CancellationToken cancellationToken)
    {
        // The aggregate row is seeded by CreateCustomer and backfilled by migration - its absence is a bug.
        var aggregateRecord = (await dlcsContext.CustomerStorages
            .SingleOrDefaultAsync(cs => cs.Customer == customerId && cs.Space == null, cancellationToken))
            .ThrowIfNull("aggregateRecord");

        var policyName = string.IsNullOrEmpty(aggregateRecord.StoragePolicy)
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
