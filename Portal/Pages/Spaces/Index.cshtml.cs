using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces
{
    public class Index : PageModel
    {
        private readonly IDlcsClient dlcsClient;
        
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
            await SetModels(page, pageSize);
        }

        private async Task SetModels(int page, int pageSize)
        {
            var spaces = await dlcsClient.GetSpaces(page, pageSize);
            PagerValues = new PagerValues(spaces.TotalItems, page, pageSize);
            SpaceModels = spaces.Members.Select(s => new SpaceModel
            {
                SpaceId = s.ModelId ?? -1,
                Name = s.Name,
                Created = s.Created ?? DateTime.MinValue
            });
        }

        public async Task<IActionResult> OnPostAsync(string newSpaceName)
        {
            // TODO - check name not taken, requires getSpaceByName
            var space = new Space { Name = newSpaceName };
            try
            {
                var newSpace = await dlcsClient.CreateSpace(space);
                int? spaceId = newSpace.ModelId;
                if (spaceId <= 0) spaceId = newSpace.GetLastPathElementAsInt();
                TempData["new-space-name"] = newSpace.Name;
                return RedirectToPage("/Spaces/Details", new { id = spaceId });
            }
            catch (DlcsException dlcsException)
            {
                TempData["error-message"] = dlcsException.Message;
            }

            await SetModels(1, PagerViewComponent.DefaultPageSize);
            return Page();
        }
    }
}