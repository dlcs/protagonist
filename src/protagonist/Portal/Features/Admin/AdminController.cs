using System;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Client;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.Admin.Requests;

namespace Portal.Features.Admin;

[Route("[controller]/[action]")]
[Authorize(Roles=ClaimsPrincipalUtils.Roles.Admin)]
[ApiController]
public class AdminController : Controller
{
    private readonly ClaimsPrincipal currentUser;
    private readonly IMediator mediator;

    public AdminController(
        ClaimsPrincipal currentUser, 
        IMediator mediator)
    {
        this.currentUser = currentUser;
        this.mediator = mediator;
    }
    
    public async Task<IActionResult> CreateSignUp([FromForm] string? note, [FromForm] DateTime? expires = null)
    {
        if(expires == null || expires.Value < DateTime.UtcNow)
        {
            expires = DateTime.UtcNow.AddDays(14);
        }
        var signUpLink = await mediator.Send(new CreateSignupLink{Note = note, Expires = expires.Value});
        TempData["new-signup-id"] = signUpLink.Id;
        return RedirectToPage("/Admin/Signups");
    }
    
    public async Task<IActionResult> DeleteSignup([FromForm] string id)
    {
        await mediator.Send(new DeleteSignupLink {Id = id});
        TempData["deleted-signup-id"] = id;
        return RedirectToPage("/Admin/Signups");
    }
}