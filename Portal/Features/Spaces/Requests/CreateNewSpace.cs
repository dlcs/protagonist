using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Client.JsonLd;
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
            private readonly ClaimsPrincipal principal;
            private readonly ILogger logger;
            private readonly IDlcsClient dlcsClient;

            public CreateNewSpaceHandler(
                IDlcsClient dlcsClient, 
                ClaimsPrincipal principal,
                ILogger<GetAllSpacesHandler> logger)
            {
                this.dlcsClient = dlcsClient;
                this.principal = principal;
                this.logger = logger;
            }
        
            public async Task<Space> Handle(CreateNewSpace request, CancellationToken cancellationToken)
            {
                var space = new Space { Name = request.NewSpaceName };
                return await dlcsClient.CreateSpace(space);
            }
        }
    
}