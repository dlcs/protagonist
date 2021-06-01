using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Spaces
{
    public class Index : PageModel
    {
        private readonly IMediator mediator;

        [BindProperty]
        public IEnumerable<SpaceModel> Spaces { get; set; }

        public class SpaceModel
        {
            public string Name { get; set; }
            public int SpaceId { get; set; }
            public DateTime Created { get; set; }
        }

        public Index(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGetAsync()
        {
            var spaces = await mediator.Send(new GetAllSpaces());
            Spaces = spaces.Select(s => new SpaceModel
            {
                SpaceId = s.Id,
                Name = s.Name,
                Created = s.Created
            });
        }

        public async Task<IActionResult> OnPostAsync(string newSpaceName)
        {
            var newSpace = await mediator.Send(new CreateNewSpace{NewSpaceName = newSpaceName});
            TempData["new-space-name"] = newSpace.Name;
            return RedirectToPage("/Spaces/Details", new { id = newSpace.Id });
        }
    }
}