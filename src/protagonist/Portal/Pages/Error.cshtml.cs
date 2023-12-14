using System;
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
        if (!Enum.IsDefined(typeof(HttpStatusCode), code))
        {
            return BadRequest();
        }

        var customMessage = GetErrorMessageIfSet();
        Message = customMessage ?? GetDefaultErrorMessage(code);
        Code = code;
        return Page();
    }
    
    public string? GetErrorMessageIfSet()
    {
        var customMessage = TempData[PageConstants.TempErrorMessageKey];
        // Ensure that TempData flags this value for deletion once it's been read
        TempData.Save();
        return customMessage?.ToString();
    }
    
    public string GetDefaultErrorMessage(HttpStatusCode code)
    {
        return ReasonPhrases.GetReasonPhrase((int)code);
    }
}