using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Users.Requests;

namespace Portal.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly IMediator mediator;

        [BindProperty]
        public IEnumerable<PortalUserModel> PortalUsers { get; set; } = Enumerable.Empty<PortalUserModel>();
        
        public class PortalUserModel
        {
            public string Email { get; set; }
            public DateTime Created { get; set; }
            public bool Enabled { get; set; }
        }
        
        public IndexModel(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGetAsync()
        {
            var portalUsers = await mediator.Send(new GetPortalUsers());
            if (!portalUsers.Member.IsNullOrEmpty())
            {
                PortalUsers = portalUsers.Member.Select(pu => new PortalUserModel
                {
                    Created = pu.Created,
                    Email = pu.Email,
                    Enabled = pu.Enabled
                });
            }
        }
    }
}