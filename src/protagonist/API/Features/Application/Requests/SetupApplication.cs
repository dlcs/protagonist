using API.Auth;
using API.Features.Customer.Requests;
using DLCS.Model;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

    public SetupApplicationHandler(DlcsContext dbContext, ApiKeyGenerator apiKeyGenerator,
        IEntityCounterRepository entityCounterRepository)
    {
        this.dbContext = dbContext;
        this.apiKeyGenerator = apiKeyGenerator;
        this.entityCounterRepository = entityCounterRepository;
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
        var updateCount = await dbContext.SaveChangesAsync(cancellationToken);

        await entityCounterRepository.Create(adminCustomer.Id, KnownEntityCounters.CustomerSpaces, adminCustomer.Id.ToString());

        return updateCount == 1
            ? CreateApiKeyResult.Success(apiKey, apiSecret)
            : CreateApiKeyResult.Fail("Error creating customer");
    }
}