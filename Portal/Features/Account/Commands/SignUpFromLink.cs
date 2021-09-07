using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Client.JsonLd;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Settings;

namespace Portal.Features.Account.Commands
{

    public class SignUpFromLink : IRequest<string>
    {
        public string SignUpCode { get; set; }
        public string CustomerSlugName { get; set; }
        public string CustomerDisplayName { get; set; }
        public string UserEmail { get; set; }
        public string UserPassword { get; set; }
    }

    public class SignUpFromLinkHandler : IRequestHandler<SignUpFromLink, string>
    {
        private readonly ILogger<SignUpFromLinkHandler> logger;
        private readonly PortalSettings options;
        private readonly DeliveratorApiAuth deliveratorApiAuth;
        private readonly DlcsContext dbContext;
        private readonly AdminDlcsClient adminDlcsClient;
        
        public SignUpFromLinkHandler(
            ILogger<SignUpFromLinkHandler> logger,
            IOptions<PortalSettings> options,
            DeliveratorApiAuth deliveratorApiAuth,
            DlcsContext dbContext,
            AdminDlcsClient adminDlcsClient)
        {
            this.logger = logger;
            this.options = options.Value;
            this.deliveratorApiAuth = deliveratorApiAuth;
            this.dbContext = dbContext;
            this.adminDlcsClient = adminDlcsClient;
        }

        public async Task<string> Handle(SignUpFromLink request, CancellationToken cancellationToken)
        {
            var admin = await dbContext.Customers.FirstAsync(c => c.Administrator, cancellationToken: cancellationToken);
            var basicAuth = deliveratorApiAuth.GetBasicAuthForCustomer(admin, options.ApiSalt);
            // This isn't right... the adminDlcsClient should come equipped with an admin-level httpclient
            // do we want to do that in startup though? We don't have the admin customer there.
            adminDlcsClient.SetBasicAuth(basicAuth);
            var requestCustomer = new Customer
            {
                Name = request.CustomerSlugName, 
                DisplayName = request.CustomerDisplayName
            };
            var newCustomer = await adminDlcsClient.CreateCustomer(requestCustomer);
            var reqPortalUser = new PortalUser
            {
                Email = request.UserEmail,
                Password = request.UserPassword,
                Enabled = true
            };
            await adminDlcsClient.CreatePortalUser(reqPortalUser, newCustomer.Id);
            
            // TODO: cancel the signUpCode now that it's been used.
            
            return "Created customer and portal user";
        }
    }
}