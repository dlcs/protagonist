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
                
        if (spaceId == 0)
        {
            storageForSpace.StoragePolicy = StoragePolicy.DefaultStoragePolicyName; // this isn't set on Customer
            // This space0 row isn't created when a customer is created, either - but should it?
        }

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
        
        var sumSummary = new CustomerStorageSummary { CustomerId = customerId };
        var space0Summary = new CustomerStorageSummary { CustomerId = customerId };
        foreach (var customerStorage in spaceStorageList)
        {
            if (customerStorage.Space == 0)
            {
                space0Summary.NumberOfStoredImages = customerStorage.NumberOfStoredImages;
                space0Summary.TotalSizeOfStoredImages = customerStorage.TotalSizeOfStoredImages;
                space0Summary.TotalSizeOfThumbnails = customerStorage.TotalSizeOfThumbnails;
            }
            else
            {
                sumSummary.NumberOfStoredImages += customerStorage.NumberOfStoredImages;
                sumSummary.TotalSizeOfStoredImages += customerStorage.TotalSizeOfStoredImages;
                sumSummary.TotalSizeOfThumbnails += customerStorage.TotalSizeOfThumbnails;
            }
        }

        // Which one of these should we return!!!
        return space0Summary;
        // return sumSummary;
    }

    public async Task<AssetStorageMetric> GetStorageMetrics(int customerId, CancellationToken cancellationToken)
    {
        var space0Record = await GetCustomerStorage(customerId, 0, true, cancellationToken);
        var policy = await policyRepository.GetStoragePolicy(space0Record.StoragePolicy, cancellationToken);
        return new AssetStorageMetric
        {
            Policy = policy.ThrowIfNull(nameof(policy))!,
            CustomerStorage = space0Record,
        };
    }
}