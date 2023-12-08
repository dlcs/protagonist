using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Portal.Pages;

public class ErrorModel : PageModel
{
    public HttpStatusCode Code { get; set; }

    public string? Message { get; set; }
        
    public IActionResult OnGet(HttpStatusCode code)
    {
        // Return Bad Request if an invalid status code is provided
        if (code == 0)
        {
            return BadRequest();
        }

        var customMessage = TempData["error-page-message"];
        Message = customMessage != null ? customMessage.ToString() : GetDefaultErrorMessage(code);
        Code = code;
        return Page();
    }
    
    public string GetDefaultErrorMessage(HttpStatusCode code)
    {
        return ReasonPhrases.GetReasonPhrase((int)code);
    }
}