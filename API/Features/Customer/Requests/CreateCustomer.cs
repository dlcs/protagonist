using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Model.Processing;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests
{
    /// <summary>
    /// Mediatr Command to Create a new Customer
    /// See Deliverator: API/Architecture/Request/API/Entities/Customers.cs
    /// </summary>
    public class CreateCustomer : IRequest<CreateCustomerResult>
    {
        /// <summary>
        /// Customer name. Will be checked for uniqueness.
        /// Used as the URL component.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Display name, must also be unique.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="displayName"></param>
        public CreateCustomer(string name, string displayName)
        {
            Name = name;
            DisplayName = displayName;
        }
    }

    public class CreateCustomerResult
    {
        public DLCS.Model.Customers.Customer? Customer;
        public List<string> ErrorMessages = new List<string>();
    }

    /// <inheritdoc />
    public class CreateCustomerHandler : IRequestHandler<CreateCustomer, CreateCustomerResult>
    {
        private readonly DlcsContext dbContext;
        private readonly ICustomerRepository customerRepository;
        private readonly IEntityCounterRepository entityCounterRepository;
        private readonly ICustomerQueueRepository customerQueueRepository;
        private readonly IAuthServicesRepository authServicesRepository;
        private readonly ILogger<CreateCustomerHandler> logger;

        public CreateCustomerHandler(
            DlcsContext dbContext,
            ICustomerRepository customerRepository,
            IEntityCounterRepository entityCounterRepository,
            ICustomerQueueRepository customerQueueRepository,
            IAuthServicesRepository authServicesRepository,
            ILogger<CreateCustomerHandler> logger)
        {
            this.dbContext = dbContext;
            this.customerRepository = customerRepository;
            this.entityCounterRepository = entityCounterRepository;
            this.customerQueueRepository = customerQueueRepository;
            this.authServicesRepository = authServicesRepository;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<CreateCustomerResult> Handle(CreateCustomer request, CancellationToken cancellationToken)
        {
            // Reproducing POST behaviour for customer in Deliverator
            // what gets locked here?
            var result = new CreateCustomerResult();
            
            await EnsureCustomerNamesNotTaken(request, result, cancellationToken);
            if (result.ErrorMessages.Any()) return result;
            
            var newModelId = await GetIdForNewCustomer();
            result.Customer = await CreateCustomer(request, cancellationToken, newModelId);

            // create an entity counter for space IDs [CreateCustomerSpaceEntityCounterBehaviour]
            await entityCounterRepository.Create(result.Customer.Id, "space", result.Customer.Id.ToString());
            
            // create a Queue [CreateCustomerQueueBehaviour]...
            var queue = new CustomerQueue { Customer = result.Customer.Id };
            // ...and save it [UpdateQueueBehaviour]
            await customerQueueRepository.Put(queue);
            
            // Create a clickthrough auth service [CreateClickthroughAuthServiceBehaviour]
            var clickThrough = authServicesRepository.CreateAuthService(
                result.Customer.Id, String.Empty, "clickthrough", 600);
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
            await dbContext.SaveChangesAsync(cancellationToken);
            
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
                Created = DateTime.Now,
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
            var existing = await dbContext.Customers
                .SingleOrDefaultAsync(c => c.Name.Equals(request.Name, StringComparison.InvariantCultureIgnoreCase),
                    cancellationToken: cancellationToken);
            if (existing != null)
            {
                result.ErrorMessages.Add("A customer with this name (url part) already exists.");
            }

            existing = await dbContext.Customers
                .SingleOrDefaultAsync(
                    c => c.DisplayName.Equals(request.DisplayName, StringComparison.InvariantCultureIgnoreCase),
                    cancellationToken: cancellationToken);
            if (existing != null)
            {
                result.ErrorMessages.Add("A customer with this display name (label) already exists.");
            }
        }

        private async Task<int> GetIdForNewCustomer()
        {
            // Deliverator: /DLCS.Application/Behaviour/Data/GetNewCustomerIDBehaviour.cs#L25
            int newModelId;
            DLCS.Model.Customers.Customer existingCustomerWithId;
            do
            {
                var next = await entityCounterRepository.GetNext(0, "customer", "0");
                newModelId = Convert.ToInt32(next);
                existingCustomerWithId = await dbContext.Customers.SingleOrDefaultAsync(c => c.Id == newModelId);
            } while (existingCustomerWithId != null);

            return newModelId;
        }
    }
}