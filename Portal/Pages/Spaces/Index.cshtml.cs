using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Spaces.Requests;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces
{
    public class Index : PageModel
    {
        private readonly IDlcsClient dlcsClient;
        private readonly IMediator mediator;
        
        public Index(
            IDlcsClient dlcsClient
            )
        {
            this.dlcsClient = dlcsClient;
        }

        [BindProperty]
        public IEnumerable<SpaceModel>? SpaceModels { get; set; }

        public PagerValues? PagerValues { get; private set; }

        public class SpaceModel
        {
            public string? Name { get; set; }
            public int SpaceId { get; set; }
            public DateTime Created { get; set; }
        }

        
        public async Task OnGetAsync([FromQuery] int page = 1, [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
        {
            var spaces = await dlcsClient.GetSpaces(page, pageSize);
            PagerValues = new PagerValues(spaces.TotalItems, page, pageSize);
            SpaceModels = spaces.Members.Select(s => new SpaceModel
            {
                SpaceId = s.ModelId,
                Name = s.Name,
                Created = s.Created.Value
            });
        }

        public async Task<IActionResult> OnPostAsync(string newSpaceName)
        {
            var space = new Space { Name = newSpaceName };
            var newSpace = await dlcsClient.CreateSpace(space);
            int? spaceId = newSpace.ModelId;
            if (spaceId <= 0) spaceId = newSpace.GetLastPathElementAsInt();
            TempData["new-space-name"] = newSpace.Name;
            return RedirectToPage("/Spaces/Details", new { id = spaceId });
        }
    }
}