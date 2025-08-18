using API.Auth;
using API.Features.Customer.Requests;
using API.Settings;
using DLCS.Core.Settings;
using DLCS.Model;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Features.Application.Requests;

/// <summary>
/// First run setup of application - create admin Customer 
/// </summary>
public class SetupApplication : IRequest<CreateApiKeyResult>
{
}

public class SetupApplicationHandler : IRequestHandler<SetupApplication, CreateApiKeyResult>
{
    private readonly DlcsContext dbContext;
    private readonly ApiKeyGenerator apiKeyGenerator;
    private readonly IEntityCounterRepository entityCounterRepository;
    private readonly DlcsSettings dlcsSettings;

    public SetupApplicationHandler(DlcsContext dbContext, ApiKeyGenerator apiKeyGenerator,
        IEntityCounterRepository entityCounterRepository, IOptions<ApiSettings> apiOptions)
    {
        this.dbContext = dbContext;
        this.apiKeyGenerator = apiKeyGenerator;
        this.entityCounterRepository = entityCounterRepository;
        dlcsSettings = apiOptions.Value.DLCS;
    }

    public async Task<CreateApiKeyResult> Handle(SetupApplication request, CancellationToken cancellationToken)
    {
        var adminExists = await dbContext.Customers.AnyAsync(c => c.Id == 1, cancellationToken: cancellationToken);
        if (adminExists)
        {
            return CreateApiKeyResult.Fail("Admin already exists");
        }

        var adminCustomer = new DLCS.Model.Customers.Customer
        {
            Administrator = true,
            Id = 1,
            Created = DateTime.UtcNow,
            DisplayName = "Administrator",
            Name = "admin",
            AcceptedAgreement = true
        };
        var (apiKey, apiSecret) = apiKeyGenerator.CreateApiKey(adminCustomer);
        adminCustomer.Keys = new[] { apiKey };
        await dbContext.Customers.AddAsync(adminCustomer, cancellationToken);

        await CreateDefaultStoragePolicy(cancellationToken);
        var updateCount = await dbContext.SaveChangesAsync(cancellationToken);

        await entityCounterRepository.TryCreate(adminCustomer.Id, KnownEntityCounters.CustomerSpaces, adminCustomer.Id.ToString());

        return updateCount == 2
            ? CreateApiKeyResult.Success(apiKey, apiSecret)
            : CreateApiKeyResult.Fail("Error creating customer");
    }

    private async Task CreateDefaultStoragePolicy(CancellationToken cancellationToken)
    {
        var storagePolicy = new StoragePolicy
        {
            Id = StoragePolicy.DefaultStoragePolicyName,
            MaximumNumberOfStoredImages = dlcsSettings.DefaultPolicyMaxNumber,
            MaximumTotalSizeOfStoredImages = dlcsSettings.DefaultPolicyMaxSize,
        };
        await dbContext.StoragePolicies.AddAsync(storagePolicy, cancellationToken);
    }
}