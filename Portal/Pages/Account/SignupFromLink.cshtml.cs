using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DLCS.Repository;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portal.Features.Account.Commands;

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
        public string? CreatedMessage { get; set; }
        
        public class InputModel
        {
            [Required(ErrorMessage = "Display name is required.")]
            public string? DisplayName { get; set; }
            
            [RegularExpression(@"^[-a-z]*$", ErrorMessage = "Url component can only have lower-case letters and hyphens, max 30 characters.")]
            [Required(ErrorMessage = "Url component is required.")]
            [StringLength(30, MinimumLength = 3)]
            public string? Slug { get; set; }
            
            [Required(ErrorMessage = "A Valid email address is required.")]
            [EmailAddress]
            public string? Email { get; set; }

            [Display(Name = "Password")]
            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string? Password { get; set; }
            
            [Display(Name = "Confirm password")]
            [Required(ErrorMessage = "Confirmation Password is required.")]
            [Compare("Password", ErrorMessage = "Password and Confirmation Password must match.")]
            [DataType(DataType.Password)]
            public string? ConfirmPassword { get; set; }
            
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

            var signupCommand = new SignUpFromLink
            {
                CustomerDisplayName = Input.DisplayName,
                CustomerSlugName = Input.Slug,
                UserEmail = Input.Email,
                UserPassword = Input.Password,
                SignUpCode = signupCode
            };
            // error handling here
            CreatedMessage = await mediator.Send(signupCommand);
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