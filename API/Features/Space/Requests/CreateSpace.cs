using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Customers;
using DLCS.Model.Spaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Space.Requests
{
    /// <summary>
    /// See Deliverator: API/Architecture/Request/API/Entities/CustomerSpaces.cs
    /// </summary>
    public class CreateSpace : IRequest<DLCS.Model.Spaces.Space>
    {
        public string Name { get; set; }
        public int Customer { get; set; }
        public string? ImageBucket { get; set; } = string.Empty;
        public string[]? Tags { get; set; }
        public string[]? Roles { get; set; }
        public int? MaxUnauthorised { get; set; }

        public CreateSpace(int customer, string name)
        {
            Customer = customer;
            Name = name;
        }
    }


    public class CreateSpaceHandler : IRequestHandler<CreateSpace, DLCS.Model.Spaces.Space>
    {
        private readonly ISpaceRepository spaceRepository;
        private readonly ICustomerRepository customerRepository;
        private readonly ILogger<CreateSpaceHandler> logger;

        public CreateSpaceHandler(
            ISpaceRepository spaceRepository,
            ICustomerRepository customerRepository,
            ILogger<CreateSpaceHandler> logger)
        {
            this.spaceRepository = spaceRepository;
            this.customerRepository = customerRepository;
            this.logger = logger;
            
        }
        
        public async Task<DLCS.Model.Spaces.Space> Handle(CreateSpace request, CancellationToken cancellationToken)
        {
            await ValidateRequest(request);
            var existing = await spaceRepository.GetSpace(request.Customer, request.Name, cancellationToken);
            if (existing != null)
            {
                throw new BadRequestException("A space with this name already exists.");
            }

            var newSpace = await spaceRepository.CreateSpace(
                request.Customer, request.Name, request.ImageBucket, 
                request.Tags, request.Roles, request.MaxUnauthorised,
                cancellationToken);

            return newSpace;
        }
        
        private async Task ValidateRequest(CreateSpace request)
        {
            var customer = await customerRepository.GetCustomer(request.Customer);
            if (customer == null)
            {
                throw new BadRequestException("Space must be created for an existing Customer.");
            }
        }
    }
}