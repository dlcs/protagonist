using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Index = Portal.Pages.Spaces.Index;

namespace Portal.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IDlcsClient dlcsClient;
        private readonly ILogger<IndexModel> _logger;
        
        [BindProperty]
        public List<Index.SpaceModel> Spaces { get; set; }
        

        public IndexModel(
            IDlcsClient dlcsClient,
            ILogger<IndexModel> logger)
        {
            this.dlcsClient = dlcsClient;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var spaces = await dlcsClient.GetSpaces(1, 1);
                // we're not actually displaying these at the moment. Just seeing if any exist for UI.
                Spaces = spaces.Members.Select(s => new Index.SpaceModel
                {
                    SpaceId = s.ModelId ?? -1,
                    Name = s.Name
                }).ToList();
            }
            else
            {
                Spaces = new List<Index.SpaceModel>(0);
            }
        }
    }
}