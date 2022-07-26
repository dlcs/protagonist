using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Users.Requests;

namespace Portal.Pages.Users;

public class IndexModel : PageModel
{
    private readonly IMediator mediator;

    [BindProperty]
    public IEnumerable<PortalUserModel> PortalUsers { get; set; } = Enumerable.Empty<PortalUserModel>();
    
    [TempData]
    public string ErrorMessage { get; set; }
    
    [TempData]
    public string SuccessMessage { get; set; }
    
    [BindProperty]
    public NewUserModel Input { get; set; }
    
    public class PortalUserModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public DateTime? Created { get; set; }
        public bool? Enabled { get; set; }
    }

    public class NewUserModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
    
    public IndexModel(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    public async Task OnGetAsync()
    {
        var portalUsers = await mediator.Send(new GetPortalUsers());
        if (!portalUsers.Members.IsNullOrEmpty())
        {
            PortalUsers = portalUsers.Members.Select(pu => new PortalUserModel
            {
                Id = pu.GetLastPathElement(),
                Created = pu.Created,
                Email = pu.Email,
                Enabled = pu.Enabled
            });
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var newPortalUser = await mediator.Send(new CreatePortalUser(Input.Email, Input.Password));
        if (newPortalUser == null)
        {
            ErrorMessage = "Error creating portal user - does the email address already exist?";
        }
        else
        {
            SuccessMessage = $"New Portal user '{newPortalUser.Email}' created";
        }

        return RedirectToPage("/Users/Index");
    }
}