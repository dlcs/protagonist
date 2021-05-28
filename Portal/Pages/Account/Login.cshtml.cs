using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Account.Commands;

namespace Portal.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IMediator mediator;

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
            
            // TODO - conditional validation
            public string ApiKey { get; set; }
            
            [DataType(DataType.Password)]
            public string ApiSecret { get; set; }
        }

        public LoginModel(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
            
            // TODO Clear the existing external cookie? or send to /home?
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            
            ReturnUrl = returnUrl;
        }
        
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid) return Page();

            var loginCommand = new LoginPortalUser
            {
                Username = Input.Email,
                Password = Input.Password,
                ApiKey = Input.ApiKey,
                ApiSecret = Input.ApiSecret
            };
            var loginResult = await mediator.Send(loginCommand);

            if (!loginResult)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // TODO - make this reusable
            if (Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect(Url.Page("/Index"));
        }
    }
}