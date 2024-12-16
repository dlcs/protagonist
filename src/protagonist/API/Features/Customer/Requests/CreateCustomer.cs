using System.Data;
using API.Infrastructure.Messaging;
using API.Infrastructure.Requests;
using API.Infrastructure.Requests.Pipelines;
using DLCS.Core;
using DLCS.Model;
using DLCS.Model.Auth;
using DLCS.Model.Processing;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Create a new Customer
/// </summary>
public class CreateCustomer : IRequest<ModifyEntityResult<DLCS.Model.Customers.Customer>>, IInvalidateCaches
{
    /// <summary>
    /// Customer name. Will be checked for uniqueness.
    /// Used as the URL component.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Display name, must also be unique.
    /// </summary>
    public string DisplayName { get; }
    
    public CreateCustomer(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }

    public string[] InvalidatedCacheKeys => new[] { CacheKeys.CustomerIdLookup, CacheKeys.CustomerNameLookup };
}

public class CreateCustomerHandler : IRequestHandler<CreateCustomer,  ModifyEntityResult<DLCS.Model.Customers.Customer>>
{
    private readonly DlcsContext dbContext;
    private readonly IEntityCounterRepository entityCounterRepository;
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly DapperNewCustomerDeliveryChannelRepository deliveryChannelPolicyRepository;
    private readonly ICustomerNotificationSender customerNotificationSender;

    public CreateCustomerHandler(
        DlcsContext dbContext,
        IEntityCounterRepository entityCounterRepository,
        IAuthServicesRepository authServicesRepository,
        DapperNewCustomerDeliveryChannelRepository deliveryChannelPolicyRepository,
        ICustomerNotificationSender customerNotificationSender)
    {
        this.dbContext = dbContext;
        this.entityCounterRepository = entityCounterRepository;
        this.authServicesRepository = authServicesRepository;
        this.deliveryChannelPolicyRepository = deliveryChannelPolicyRepository;
        this.customerNotificationSender = customerNotificationSender;
    }

    public async Task<ModifyEntityResult<DLCS.Model.Customers.Customer>> Handle(CreateCustomer request, CancellationToken cancellationToken)
    {
        var customerNameError = await EnsureCustomerNamesNotTaken(request, cancellationToken);
        if (customerNameError != null)
        {
            return ModifyEntityResult<DLCS.Model.Customers.Customer>.Failure(customerNameError, WriteResult.Conflict);
        }

        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        
        var customer = await CreateCustomer(request, cancellationToken);
        var newCustomerId = customer.Id;

        // create an entity counter for space IDs
        await entityCounterRepository.Create(newCustomerId, KnownEntityCounters.CustomerSpaces, newCustomerId.ToString());

        await CreateAuthServices(cancellationToken, newCustomerId);

        // Create both a default and priority queue
        await dbContext.Queues.AddRangeAsync(
            new Queue { Customer = newCustomerId, Name = QueueNames.Default, Size = 0 },
            new Queue { Customer = newCustomerId, Name = QueueNames.Priority, Size = 0 }
        );

        await dbContext.SaveChangesAsync(cancellationToken);

        var deliveryChannelPoliciesCreated = await deliveryChannelPolicyRepository.SeedDeliveryChannelsData(newCustomerId);

        if (deliveryChannelPoliciesCreated)
        {
            await transaction.CommitAsync(cancellationToken);
            await customerNotificationSender.SendCustomerCreatedMessage(customer, cancellationToken);
            return ModifyEntityResult<DLCS.Model.Customers.Customer>.Success(customer, WriteResult.Created);
        }
        
        await transaction.RollbackAsync(cancellationToken);

        return ModifyEntityResult<DLCS.Model.Customers.Customer>.Failure("Failed to create customer",
            WriteResult.Error);
    }

    private async Task CreateAuthServices(CancellationToken cancellationToken, int newCustomerId)
    {
        // Create a clickthrough auth service
        var clickThrough = authServicesRepository.CreateAuthService(newCustomerId, string.Empty, "clickthrough", 600);
        // Create a logout auth service
        var logout =
            authServicesRepository.CreateAuthService(newCustomerId, "http://iiif.io/api/auth/1/logout", "logout", 600);
        clickThrough.ChildAuthService = logout.Id;
        
        // Make a Role for clickthrough
        var clickthroughRole = authServicesRepository.CreateRole("clickthrough", newCustomerId, clickThrough.Id);
        
        await dbContext.AuthServices.AddAsync(clickThrough, cancellationToken);
        await dbContext.AuthServices.AddAsync(logout, cancellationToken);
        await dbContext.Roles.AddAsync(clickthroughRole, cancellationToken);
    }

    private async Task<DLCS.Model.Customers.Customer> CreateCustomer(CreateCustomer request,
        CancellationToken cancellationToken)
    {
        var newModelId = await GetIdForNewCustomer();
        var customer = new DLCS.Model.Customers.Customer
        {
            Id = newModelId,
            Name = request.Name,
            DisplayName = request.DisplayName,
            Administrator = false,
            Created = DateTime.UtcNow,
            AcceptedAgreement = true,
            Keys = Array.Empty<string>()
        };

        await dbContext.Customers.AddAsync(customer, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private async Task<string?> EnsureCustomerNamesNotTaken(CreateCustomer request, CancellationToken cancellationToken)
    {
        // This could use customerRepository.GetCustomer(request.Name), but we want to be a bit more restrictive.
        var allCustomers = await dbContext.Customers.ToListAsync(cancellationToken);
        // get all locally for more string comparison support
        var existing = allCustomers.SingleOrDefault(c 
            => c.Name.Equals(request.Name, StringComparison.InvariantCultureIgnoreCase));
        if (existing != null)
        {
            return "A customer with this name (url part) already exists.";
        }

        existing = allCustomers.SingleOrDefault(
                c => c.DisplayName.Equals(request.DisplayName, StringComparison.InvariantCultureIgnoreCase));
        if (existing != null)
        {
            return "A customer with this display name (label) already exists.";
        }

        return null;
    }

    private async Task<int> GetIdForNewCustomer()
    {
        int newModelId;
        DLCS.Model.Customers.Customer? existingCustomerWithId;
        do
        {
            var next = await entityCounterRepository.GetNext(0, KnownEntityCounters.Customers, "0");
            newModelId = Convert.ToInt32(next);
            existingCustomerWithId = await dbContext.Customers.SingleOrDefaultAsync(c => c.Id == newModelId);
        } while (existingCustomerWithId != null);

        return newModelId;
    }
}