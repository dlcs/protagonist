using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Space.Requests;
using API.Settings;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Space
{
    [Route("/customers/{customerId}/spaces")]
    [ApiController]
    public class SpaceController : Controller
    {
        private readonly IMediator mediator;
        private readonly ApiSettings settings;

        public SpaceController(
            IMediator mediator,
            IOptions<ApiSettings> options)
        {
            this.mediator = mediator;
            settings = options.Value;
        }
        
        
        [HttpGet]
        public async Task<HydraCollection<DLCS.HydraModel.Space>> Index(int customerId, int page = 1, int pageSize = -1)
        {
            if (pageSize < 0) pageSize = settings.PageSize;
            var baseUrl = Request.GetBaseUrl();
            var pageOfSpaces = await mediator.Send(new GetPageOfSpaces(page, pageSize, customerId));
            
            var collection = new HydraCollection<DLCS.HydraModel.Space>
            {
                IncludeContext = true,
                Members = pageOfSpaces.Spaces.Select(s => s.ToHydra(baseUrl)).ToArray(),
                TotalItems = pageOfSpaces.Total,
                PageSize = pageSize,
                Id = Request.GetJsonLdId()
            };
            PartialCollectionView.AddPaging(collection, page, pageSize);
            return collection;
        }
    }
}