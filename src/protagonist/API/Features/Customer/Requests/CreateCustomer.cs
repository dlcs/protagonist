using System.Collections.Generic;
using System.Data;
using API.Infrastructure.Messaging;
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
public class CreateCustomer : IRequest<CreateCustomerResult>
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
}

public class CreateCustomerResult
{
    public DLCS.Model.Customers.Customer? Customer;
    public List<string> ErrorMessages = new();
    public bool Conflict { get; set; }
}

public class CreateCustomerHandler : IRequestHandler<CreateCustomer, CreateCustomerResult>
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

    public async Task<CreateCustomerResult> Handle(CreateCustomer request, CancellationToken cancellationToken)
    {
        // Reproducing POST behaviour for customer in Deliverator
        // what gets locked here?
        var result = new CreateCustomerResult();
        
        await EnsureCustomerNamesNotTaken(request, result, cancellationToken);
        if (result.ErrorMessages.Any()) return result;
        
        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        
        var newModelId = await GetIdForNewCustomer();
        result.Customer = await CreateCustomer(request, cancellationToken, newModelId);

        // create an entity counter for space IDs [CreateCustomerSpaceEntityCounterBehaviour]
        await entityCounterRepository.Create(result.Customer.Id, KnownEntityCounters.CustomerSpaces, result.Customer.Id.ToString());

        // Create a clickthrough auth service [CreateClickthroughAuthServiceBehaviour]
        var clickThrough = authServicesRepository.CreateAuthService(
            result.Customer.Id, string.Empty, "clickthrough", 600);
        // Create a logout auth service [CreateLogoutAuthServiceBehaviour]
        var logout = authServicesRepository.CreateAuthService(
            result.Customer.Id, "http://iiif.io/api/auth/1/logout", "logout", 600);
        clickThrough.ChildAuthService = logout.Id;
        
        // Make a Role for clickthrough [CreateClickthroughRoleBehaviour]
        var clickthroughRole = authServicesRepository.CreateRole("clickthrough", result.Customer.Id, clickThrough.Id);
        
        // Save these [UpdateAuthServiceBehaviour x2, UpdateRoleBehaviour]
        // Like this?
        // authServicesRepository.SaveAuthService(clickThrough);
        // authServicesRepository.SaveAuthService(logout);
        // authServicesRepository.SaveRole(clickthroughRole);
        // or like this?
        await dbContext.AuthServices.AddAsync(clickThrough, cancellationToken);
        await dbContext.AuthServices.AddAsync(logout, cancellationToken);
        await dbContext.Roles.AddAsync(clickthroughRole, cancellationToken);
        
        // Create both a default and priority queue
        await dbContext.Queues.AddRangeAsync(
            new Queue { Customer = result.Customer.Id, Name = QueueNames.Default, Size = 0 },
            new Queue { Customer = result.Customer.Id, Name = QueueNames.Priority, Size = 0 }
        );

        await dbContext.SaveChangesAsync(cancellationToken);

        var deliveryChannelPoliciesCreated = await deliveryChannelPolicyRepository.SeedDeliveryChannelsData(result.Customer.Id);

        if (deliveryChannelPoliciesCreated)
        {
            await transaction.CommitAsync(cancellationToken);
            await customerNotificationSender.SendCustomerCreatedMessage(result.Customer, cancellationToken);
            return result;
        }
        
        result = new CreateCustomerResult()
        {
            ErrorMessages = new List<string>()
            {
                "Failed to create customer"
            }
        };
        
        await transaction.RollbackAsync(cancellationToken);
        

        // [UpdateCustomerBehaviour] - customer has already been saved.
        // The problem here is that we have had:
        // - some direct use of dbContext
        // - some calls to repositories that use EF (and do their own SaveChanges)
        // - some calls to repositories that use Dapper
        
        return result;
    }

    // Does this belong on ICustomerRepository?
    private async Task<DLCS.Model.Customers.Customer> CreateCustomer(
        CreateCustomer request, 
        CancellationToken cancellationToken, 
        int newModelId)
    {
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

    private async Task EnsureCustomerNamesNotTaken(CreateCustomer request, CreateCustomerResult result, CancellationToken cancellationToken)
    {
        // This could use customerRepository.GetCustomer(request.Name), but we want to be a bit more restrictive.
        var allCustomers = await dbContext.Customers.ToListAsync(cancellationToken);
        // get all locally for more string comparison support
        var existing = allCustomers.SingleOrDefault(c 
            => c.Name.Equals(request.Name, StringComparison.InvariantCultureIgnoreCase));
        if (existing != null)
        {
            result.Conflict = true;
            result.ErrorMessages.Add("A customer with this name (url part) already exists.");
        }

        existing = allCustomers.SingleOrDefault(
                c => c.DisplayName.Equals(request.DisplayName, StringComparison.InvariantCultureIgnoreCase));
        if (existing != null)
        {
            result.Conflict = true;
            result.ErrorMessages.Add("A customer with this display name (label) already exists.");
        }
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