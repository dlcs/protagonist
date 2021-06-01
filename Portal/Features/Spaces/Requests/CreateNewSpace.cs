using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Spaces.Requests
{
        public class CreateNewSpace : IRequest<Space>
        {
            public string? NewSpaceName { get; set; }
        }

        public class CreateNewSpaceHandler : IRequestHandler<CreateNewSpace, Space>
        {
            private readonly DlcsContext dbContext;
            private readonly ClaimsPrincipal principal;
            private readonly ILogger logger;

            public CreateNewSpaceHandler(
                DlcsContext dbContext, 
                ClaimsPrincipal principal,
                ILogger<GetAllSpacesHandler> logger)
            {
                this.dbContext = dbContext;
                this.principal = principal;
                this.logger = logger;
            }
        
            public async Task<Space> Handle(CreateNewSpace request, CancellationToken cancellationToken)
            {
                var customer = principal.GetCustomerId();
                if (!customer.HasValue)
                {
                    throw new NotSupportedException("No customer to create space for");
                }

                var newSpace = await dbContext.Spaces.AddAsync(new Space
                {
                    Name = request.NewSpaceName,
                    Created = DateTime.Now,
                    Customer = customer.Value,
                    ImageBucket = String.Empty,
                    Tags = String.Empty,
                    Roles = String.Empty
                });
                await dbContext.SaveChangesAsync(cancellationToken);
                return newSpace.Entity;
            }
        }
    
}