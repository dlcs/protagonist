using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Account.Commands;
using Portal.Features.Account.Models;
using Portal.Features.Admin;
using Portal.Features.Admin.Commands;

namespace Portal.Pages.Admin
{
    public class Signups : PageModel
    {
        private readonly IMediator mediator;

        [BindProperty]
        public IEnumerable<SignupModel> SignupLinks { get; set; }
        
        public Signups(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGetAsync()
        {
            SignupLinks = await mediator.Send(new GetAllSignupLinks());
            foreach (var signup in SignupLinks)
            {
                if (signup.CustomerName != null)
                {
                    signup.CssClass = "activated";
                }
                else if (signup.Expires < DateTime.Now)
                {
                    signup.CssClass = "expired";
                }
                else
                {
                    signup.CssClass = "";
                }
            }
        }
    }
}