using System.Linq;
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

    public async Task<CustomerStorage?> GetCustomerStorage(int customerId, int spaceId, bool createOnDemand,
        CancellationToken cancellationToken)
    {
        // TODO - periodically recalculate this as-per Deliverator
        var storageForSpace =
            await dlcsContext.CustomerStorages.SingleOrDefaultAsync(cs =>
                cs.Customer == customerId && cs.Space == spaceId, cancellationToken: cancellationToken);

        if (storageForSpace != null) return storageForSpace;

        if (!createOnDemand) return storageForSpace;

        storageForSpace = new CustomerStorage
        {
            Customer = customerId, Space = spaceId, StoragePolicy = string.Empty,
            NumberOfStoredImages = 0, TotalSizeOfThumbnails = 0, TotalSizeOfStoredImages = 0
        };

        await dlcsContext.CustomerStorages.AddAsync(storageForSpace, cancellationToken);
        await dlcsContext.SaveChangesAsync(cancellationToken);

        return storageForSpace;
    }

    public async Task<CustomerStorageSummary> GetCustomerStorageSummary(
        int customerId, CancellationToken cancellationToken)
    {
        // Is it quicker to do this with a SUM in the database? Depends how many spaces the customer has.
        var spaceStorageList = await dlcsContext.CustomerStorages
            .Where(cs => cs.Customer == customerId)
            .ToListAsync(cancellationToken);
        
        var aggregateSummary = new CustomerStorageSummary { CustomerId = customerId };
        foreach (var customerStorage in spaceStorageList)
        {
            if (customerStorage.Space == null)
            {
                aggregateSummary.NumberOfStoredImages = customerStorage.NumberOfStoredImages;
                aggregateSummary.TotalSizeOfStoredImages = customerStorage.TotalSizeOfStoredImages;
                aggregateSummary.TotalSizeOfThumbnails = customerStorage.TotalSizeOfThumbnails;
            }
        }

        return aggregateSummary;
    }

    public async Task<AssetStorageMetric> GetStorageMetrics(int customerId, CancellationToken cancellationToken)
    {
        var aggregateRecord = await dlcsContext.CustomerStorages
            .SingleOrDefaultAsync(cs => cs.Customer == customerId && cs.Space == null, cancellationToken);

        if (aggregateRecord == null)
        {
            aggregateRecord = new CustomerStorage
            {
                Customer = customerId, Space = null,
                StoragePolicy = StoragePolicy.DefaultStoragePolicyName,
                NumberOfStoredImages = 0, TotalSizeOfThumbnails = 0, TotalSizeOfStoredImages = 0
            };
            await dlcsContext.CustomerStorages.AddAsync(aggregateRecord, cancellationToken);
            await dlcsContext.SaveChangesAsync(cancellationToken);
        }

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