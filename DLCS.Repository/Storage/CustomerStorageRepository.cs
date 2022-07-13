using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Storage;

public class CustomerStorageRepository : IStorageRepository
{
    private readonly DlcsContext dlcsContext;
    private readonly IPolicyRepository policyRepository;

    public CustomerStorageRepository(DlcsContext dlcsContext, IPolicyRepository policyRepository)
    {
        this.dlcsContext = dlcsContext;
        this.policyRepository = policyRepository;
    }

    public async Task<CustomerStorage?> GetCustomerStorage(int customerId, int spaceId, bool createOnDemand, CancellationToken cancellationToken)
    {
        var storageForSpace =
            await dlcsContext.CustomerStorages.SingleOrDefaultAsync(cs =>
                cs.Customer == customerId && cs.Space == spaceId, cancellationToken: cancellationToken);
        if (storageForSpace == null)
        {
            if (createOnDemand)
            {
                storageForSpace = new CustomerStorage
                {
                    Customer = customerId, Space = spaceId, StoragePolicy = String.Empty,
                    NumberOfStoredImages = 0, TotalSizeOfThumbnails = 0, TotalSizeOfStoredImages = 0
                };
                if (spaceId == 0)
                {
                    storageForSpace.StoragePolicy = "default"; // this isn't set on Customer
                    // This space0 row isn't created when a customer is created, either - but should it?
                }

                await dlcsContext.CustomerStorages.AddAsync(storageForSpace, cancellationToken);
                await dlcsContext.SaveChangesAsync(cancellationToken);
            }

            return storageForSpace;
        }

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