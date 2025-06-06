﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages.Account;

public class SignedOutModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity.IsAuthenticated)
        {
            // Redirect to home page if the user is authenticated.
            return RedirectToPage("/Index");
        }

        return Page();
    }
}