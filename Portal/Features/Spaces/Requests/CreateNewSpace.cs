using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Microsoft.Extensions.Logging;
using Portal.Legacy;

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
            private readonly DlcsClient dlcsClient;

            public CreateNewSpaceHandler(
                DlcsClient dlcsClient, 
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