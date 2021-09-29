using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Portal.Features.Spaces.Requests;
using Index = Portal.Pages.Spaces.Index;

namespace Portal.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IMediator mediator;
        private readonly ILogger<IndexModel> _logger;
        
        [BindProperty]
        public List<Index.SpaceModel> Spaces { get; set; }

        public IndexModel(IMediator mediator, ILogger<IndexModel> logger)
        {
            this.mediator = mediator;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {
                var spaces = await mediator.Send(new GetAllSpaces());
                Spaces = spaces.Select(s => new Index.SpaceModel
                {
                    SpaceId = s.Id,
                    Name = s.Name,
                    Created = s.Created
                }).ToList();
            }
            else
            {
                Spaces = new List<Index.SpaceModel>(0);
            }
        }
    }
}