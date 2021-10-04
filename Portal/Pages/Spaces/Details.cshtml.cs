using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Client;
using API.Client.JsonLd;
using DLCS.Core.Settings;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Models;
using Portal.Features.Spaces.Requests;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces
{
    [BindProperties]
    public class Details : PageModel
    {
        private readonly IMediator mediator;
        
        public DlcsSettings DlcsSettings { get; }
        
        public SpacePageModel SpacePageModel { get; set; }
        
        public PagerValues? PagerValues { get; private set; }
        
        public string Customer { get; set; }
        
        public string Space { get; set; }

        public Details(
            IMediator mediator,
            IOptions<DlcsSettings> dlcsSettings,
            ClaimsPrincipal currentUser)
        {
            this.mediator = mediator;
            DlcsSettings = dlcsSettings.Value;
            Customer = (currentUser.GetCustomerId() ?? -1).ToString();
        }
        
        public async Task<IActionResult> OnGetAsync(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
        {
            Space = id.ToString();
            // At the moment the DLCS API does not provide the option of setting the page size.
            // So the page size coming back from the Hydra API will override whatever value is set on the query string.
            // See http://www.hydra-cg.com/spec/latest/core/#example-20-a-hydra-partialcollectionview-splits-a-collection-into-multiple-views
            SpacePageModel = await mediator.Send(new GetSpaceDetails(id, page, pageSize, nameof(Image.Number1)));

            if (SpacePageModel.Space == null)
            {
                return NotFound();
            }

            SetPager(page, pageSize);
            return Page();
        }

        private void SetPager(int page, int pageSize)
        {
            var partialImageCollection = SpacePageModel.Images;
            PagerValues = new PagerValues(
                partialImageCollection?.TotalItems ?? 0,
                page,
                partialImageCollection?.PageSize ?? pageSize);
        }

        public async Task<IActionResult> OnPostConvert(int spaceId)
        {
            var manifestMode = Request.Form.ContainsKey("manifest-mode");
            // TODO - handle failure
            var result = await mediator.Send(new ToggleManifestMode(spaceId, manifestMode));

            return RedirectToPage("/spaces/details", new {id = spaceId});
        }

        public async Task<IActionResult> OnPostReOrder(int spaceId, [FromQuery] int page = 1, [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
        {
            var orderDict = new Dictionary<string, int>();
            const string rowIdPrefix = "row-id-";
            const string rowIndexPrefix = "row-index-";
            foreach (var key in Request.Form.Keys)
            {
                if (key.StartsWith(rowIdPrefix))
                {
                    string imageId = Request.Form[key];
                    string rowIndex = key.Substring(rowIdPrefix.Length);
                    int order = int.Parse(Request.Form["row-index-" + rowIndex]);
                    orderDict[imageId] = order;
                }
            }
            SpacePageModel = await mediator.Send(new GetSpaceDetails(spaceId, page, pageSize));
            var images = SpacePageModel.Images;
            if (images != null)
            {
                var highest = orderDict.Values.Max();
                foreach (Image image in images.Members)
                {
                    if (orderDict.ContainsKey(image.Id))
                    {
                        image.Number1 = orderDict[image.Id];
                    }
                    else
                    {
                        image.Number1 = ++highest;
                    }
                }

                var patchRequest = new PatchImages {Images = images, SpaceId = spaceId};
                await mediator.Send(patchRequest);
            }

            return RedirectToPage("/spaces/details", new {id = spaceId});
        }
    }
}