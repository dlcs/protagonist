using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Threading;
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
    private readonly AsyncKeyedLock asyncLocker = new();

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
            storageForSpace.StoragePolicy = "default"; // this isn't set on Customer
            // This space0 row isn't created when a customer is created, either - but should it?
        }

        await dlcsContext.CustomerStorages.AddAsync(storageForSpace, cancellationToken);
        await dlcsContext.SaveChangesAsync(cancellationToken);

        return storageForSpace;

    }

    public async Task<bool> VerifyStoragePolicyBySize(int customer, long proposedNewFileSize,
        CancellationToken cancellationToken = default)
    {
        var key = $"verify:{customer}";
        using var processLock = await asyncLocker.LockAsync(key, cancellationToken);
        try
        {
            var customerStorage = await GetCustomerStorage(customer, 0, true, cancellationToken);
            if (customerStorage == null || string.IsNullOrEmpty(customerStorage.StoragePolicy))
            {
                throw new ApplicationException(
                    $"CustomerStorage for Customer {customer}, Space 0 not found or does not have a storage policy");
            }
            
            var policy = await policyRepository.GetStoragePolicy(customerStorage.StoragePolicy, cancellationToken);
            return customerStorage.TotalSizeOfStoredImages + proposedNewFileSize <= policy.MaximumTotalSizeOfStoredImages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error verifying storage policy for customer: {Customer}. New asset size: {Size}",
                customer, proposedNewFileSize);
            return false;
        }
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

    public async Task<ImageCountStorageMetric> GetImageCounts(int customerId, CancellationToken cancellationToken)
    {
        var space0Record = await GetCustomerStorage(customerId, 0, true, cancellationToken);
        var policy = await policyRepository.GetStoragePolicy(space0Record.StoragePolicy, cancellationToken);
        return new ImageCountStorageMetric
        {
            PolicyId = policy.Id,
            MaximumNumberOfStoredImages = policy.MaximumNumberOfStoredImages,
            CurrentNumberOfStoredImages = space0Record.NumberOfStoredImages,
            MaximumTotalSizeOfStoredImages = policy.MaximumTotalSizeOfStoredImages,
            CurrentTotalSizeStoredImages = space0Record.TotalSizeOfStoredImages
        };
    }
}