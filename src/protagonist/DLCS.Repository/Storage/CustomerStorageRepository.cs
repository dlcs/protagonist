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

public class CustomerStorageRepository(
    DlcsContext dlcsContext,
    IPolicyRepository policyRepository,
    ILogger<CustomerStorageRepository> logger)
    : IStorageRepository
{
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

    public async Task<bool> DeleteCustomerStorage(int customer, int space, CancellationToken cancellationToken)
    {
        var deleted = await dlcsContext.CustomerStorages
            .Where(cs => cs.Customer == customer && cs.Space == space)
            .DeleteFromQueryAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task TryCreateCustomerStorage(int customer, int? space,
        string policy = StoragePolicy.DefaultStoragePolicyName, CancellationToken cancellationToken = default)
    {
        var exists = await dlcsContext.CustomerStorages.AnyAsync(cs =>
            cs.Customer == customer && cs.Space == space, cancellationToken: cancellationToken);

        if (exists)
        {
            // NOTE - CustomerStorage creation on Space creation was added after some instances have been running for a 
            // long time, so some instances may have duplicate rows. This is not a bug!
            logger.LogWarning("CustomerStorage already exists for customer {CustomerId} and space {SpaceId}", customer,
                space);
            return;
        }

        await dlcsContext.CustomerStorages.AddAsync(new CustomerStorage
        {
            Customer = customer,
            Space = space,
            StoragePolicy = policy,
        }, cancellationToken);
        await dlcsContext.SaveChangesAsync(cancellationToken);
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
