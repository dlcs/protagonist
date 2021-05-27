using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        }

        public Index(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGet()
        {
            var spaces = await mediator.Send(new GetAllSpaces());
            Spaces = spaces.Select(s => new SpaceModel {SpaceId = s.Id, Name = s.Name});
        }
    }
}