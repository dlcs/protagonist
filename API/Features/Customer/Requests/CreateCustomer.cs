using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests
{
    /// <summary>
    /// See Deliverator: API/Architecture/Request/API/Entities/Customers.cs
    /// </summary>
    public class CreateCustomer : IRequest<DLCS.Model.Customers.Customer>
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }

        public CreateCustomer(string name, string displayName)
        {
            Name = name;
            DisplayName = displayName;
        }
    }

    public class CreateCustomerHandler : IRequestHandler<CreateCustomer, DLCS.Model.Customers.Customer>
    {
        private readonly DlcsContext dbContext;
        private readonly IEntityCounterRepository entityCounterRepository;
        private readonly ILogger<CreateCustomerHandler> logger;

        public CreateCustomerHandler(
            DlcsContext dbContext,
            IEntityCounterRepository entityCounterRepository,
            ILogger<CreateCustomerHandler> logger)
        {
            this.dbContext = dbContext;
            this.entityCounterRepository = entityCounterRepository;
            this.logger = logger;
        }
        
        public async Task<DLCS.Model.Customers.Customer> Handle(CreateCustomer request, CancellationToken cancellationToken)
        {
            // Reproducing POST behaviour for customer in Deliverator
            // what gets locked here?
            
            var existing = await dbContext.Customers
                .SingleOrDefaultAsync(c => c.Name.Equals(request.Name, StringComparison.InvariantCultureIgnoreCase),
                    cancellationToken: cancellationToken);
            if (existing != null)
            {
                throw new BadRequestException("A customer with this name (url part) already exists.");
            }
            existing = await dbContext.Customers
                .SingleOrDefaultAsync(c => c.DisplayName.Equals(request.DisplayName, StringComparison.InvariantCultureIgnoreCase),
                    cancellationToken: cancellationToken);
            if (existing != null)
            {
                throw new BadRequestException("A customer with this display name (label) already exists.");
            }

            int newModelId = 0;
            DLCS.Model.Customers.Customer existingCustomerWithId = null;
            do
            {
                newModelId = Convert.ToInt32(entityCounterRepository.GetNext(0, "customer", "0"));
                existingCustomerWithId = await dbContext.Customers.SingleOrDefaultAsync(c => c.Id == newModelId);
            }
            while (existingCustomerWithId != null);
            
            var customer = new DLCS.Model.Customers.Customer
            {
                Id = newModelId,
                Administrator = false,
                Created = DateTime.Now,
                Name = request.Name,
                DisplayName = request.DisplayName
            };

            await dbContext.Customers.AddAsync(customer, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await entityCounterRepository.Create(customer.Id, "customer", customer.Id.ToString());
            
            // Deliverator Customers.cs
            // CreateCustomerQueueBehaviour
            // UpdateQueueBehaviour
            // CreateClickthroughAuthServiceBehaviour
            // CreateLogoutAuthServiceBehaviour
            // CreateClickthroughRoleBehaviour
            // UpdateAuthServiceBehaviour
            // UpdateAuthServiceBehaviour
            // UpdateRoleBehaviour
            // UpdateCustomerBehaviour
            // etc
            
            return customer;
        }
    }
}