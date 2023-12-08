using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages;

public class ErrorModel : PageModel
{
    public HttpStatusCode Code { get; set; }
        
    public IActionResult OnGet(HttpStatusCode code)
    {
        if (code == 0)
        {
            return NotFound();
        }

        Code = code;
        return Page();
    }
}