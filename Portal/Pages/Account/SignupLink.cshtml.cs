using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DLCS.Repository;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Portal.Pages.Account
{
    public class SignupModel : PageModel
    {
        private readonly DlcsContext dbContext;
        private readonly IMediator mediator;

        public SignupModel(
            DlcsContext dbContext,
            IMediator mediator)
        {
            this.dbContext = dbContext;
            this.mediator = mediator;
        }
        
        [BindProperty]
        public InputModel? Input { get; set; }
        
        public bool ValidLink { get; set; }
        public string CreatedMessage { get; set; }
        
        public class InputModel
        {
            [Display(Name = "Display name of this account (e.g., organisation name)")]
            [Required(ErrorMessage = "Display name is required.")]
            public string DisplayName { get; set; }
            
            [Display(Name = "Account name that appears as part of a web address")]
            [RegularExpression(@"^[-a-z]*$", ErrorMessage = "Url component can only have lower-case letters and hyphens, max 30 characters.")]
            [Required(ErrorMessage = "Url component is required.")]
            [StringLength(30, MinimumLength = 3)]
            public string Slug { get; set; }
            
            [Display(Name = "Email address to log into this portal.")]
            [Required(ErrorMessage = "A Valid email address is required.")]
            [EmailAddress]
            public string Email { get; set; }

            [Display(Name = "Password")]
            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }
            
            [Display(Name = "Confirm password")]
            [Required(ErrorMessage = "Confirmation Password is required.")]
            [Compare("Password", ErrorMessage = "Password and Confirmation Password must match.")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; }
            
        }
        
        public async Task<IActionResult> OnGetAsync(string signupCode)
        {
            ValidLink = await ValidateSignupCode(signupCode);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string signupCode)
        {
            ValidLink = await ValidateSignupCode(signupCode);
            if (!ModelState.IsValid) return Page();
            if (Input == null) return Page();
            if (await EmailInUse(Input.Email))
            {
                ModelState.AddModelError("Input.Email", $"The email address {Input.Email} is already in use.");
            }
            if (await CustomerNameInUse(Input.DisplayName))
            {
                ModelState.AddModelError("Input.Email", $"The customer name {Input.DisplayName} is already taken.");
            }
            if (await SlugInUse(Input.Slug))
            {
                ModelState.AddModelError("Input.Email", $"The url component {Input.Slug} is already taken.");
            }
            if (!ModelState.IsValid) return Page();
            
            return Page();
        }

        private async Task<bool> EmailInUse(string inputEmail)
        {
            return await dbContext.Users.AnyAsync(user => user.Email == inputEmail);
        }
        
        private async Task<bool> CustomerNameInUse(string inputDisplayName)
        {
            return await dbContext.Customers.AnyAsync(customer => customer.DisplayName == inputDisplayName);
        }

        private async Task<bool> SlugInUse(string inputSlug)
        {
            return await dbContext.Customers.AnyAsync(customer => customer.Name == inputSlug);
        }




        private async Task<bool> ValidateSignupCode(string signupCode)
        {
            // go to database...
            return await Task.FromResult(true);
        }
    }
}