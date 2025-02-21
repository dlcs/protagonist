using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Keys.Requests;

namespace Portal.Pages.Keys;

public class CreateModel : PageModel
{
    private readonly IMediator mediator;

    [BindProperty]
    public NewApiKeyModel? ApiKey { get; set; }
    
    public class NewApiKeyModel
    {
        public string Key { get; set; }
        public string Secret { get; set; }
    }

    public CreateModel(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    public IActionResult OnGet()
    {
        return RedirectToPage("/Keys/Index");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var newApiKey = await mediator.Send(new CreateNewApiKey());
        ApiKey = new NewApiKeyModel {Key = newApiKey.Key, Secret = newApiKey.Secret};
        return Page();
    }
}