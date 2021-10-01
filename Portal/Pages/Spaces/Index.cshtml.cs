using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Spaces.Requests;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces
{
    public class Index : PageModel
    {
        private readonly IMediator mediator;

        [BindProperty]
        public IEnumerable<SpaceModel> SpaceModels { get; set; }
        public int TotalSpaces { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }

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
        
        public async Task OnGetAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            PageIndex = page;
            PageSize = pageSize;
            var spaces = await mediator.Send(new GetPageOfSpaces(page, pageSize));
            TotalSpaces = spaces.Total;
            SpaceModels = spaces.Spaces.Select(s => new SpaceModel
            {
                SpaceId = s.Id,
                Name = s.Name,
                Created = s.Created
            });
        }

        public async Task<IActionResult> OnPostAsync(string newSpaceName)
        {
            var newSpace = await mediator.Send(new CreateNewSpace{NewSpaceName = newSpaceName});
            int? spaceId = newSpace.ModelId;
            if (spaceId <= 0) spaceId = newSpace.GetLastPathElementAsInt();
            TempData["new-space-name"] = newSpace.Name;
            return RedirectToPage("/Spaces/Details", new { id = spaceId });
        }
    }
}