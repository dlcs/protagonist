using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages;

public class ErrorModel : PageModel
{
    public HttpStatusCode Code { get; set; }
        
    public IActionResult OnGet(HttpStatusCode code)
    {
        // Return Bad Request if an invalid status code is provided
        if (code == 0)
        {
            return BadRequest();
        }

        Code = code;
        return Page();
    }
}