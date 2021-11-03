using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Models;
using Portal.Settings;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces
{
    [BindProperties]
    public class Details : PageModel
    {
        private readonly IDlcsClient dlcsClient;
        public DlcsSettings DlcsSettings { get; }
        private readonly PortalSettings portalSettings;
        public SpacePageModel SpacePageModel { get; set; }
        public PagerValues? PagerValues { get; private set; }
        public string Customer { get; set; }
        public string Space { get; set; }

        public Details(
            IDlcsClient dlcsClient,
            IOptions<DlcsSettings> dlcsSettings,
            IOptions<PortalSettings> portalSettings,
            ClaimsPrincipal currentUser)
        {
            this.dlcsClient = dlcsClient;
            DlcsSettings = dlcsSettings.Value;
            this.portalSettings = portalSettings.Value;
            Customer = (currentUser.GetCustomerId() ?? -1).ToString();
        }
        
        public async Task<IActionResult> OnGetAsync(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
        {
            var space = await dlcsClient.GetSpaceDetails(id);
            if (space == null)
            {
                return NotFound();
            }
            var images = await dlcsClient.GetSpaceImages(page, pageSize, id, nameof(Image.Number1));
            var model = new SpacePageModel
            {
                Space = space,
                Images = images,
                IsManifestSpace = space?.IsManifestSpace() ?? false
            };

            if (model.IsManifestSpace)
            {
                SetManifestLinks(model);
            }

            SpacePageModel = model;
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

        private void SetManifestLinks(SpacePageModel model)
        {
            var namedQuery = DlcsPathHelpers.GeneratePathFromTemplate(
                DlcsSettings.SpaceManifestQuery,
                customer: User.GetCustomerId().ToString(),
                space: model.Space.ModelId.ToString());
                
            model.NamedQuery = new Uri(namedQuery);
            model.UniversalViewer = new Uri(string.Concat(portalSettings.UVUrl, "?manifest=", namedQuery));
            model.MiradorViewer = new Uri(string.Concat(portalSettings.MiradorUrl, "?manifest=", namedQuery));
        }
        
        public async Task<IActionResult> OnPostConvert(int spaceId)
        {
            var manifestMode = Request.Form.ContainsKey("manifest-mode");
            var space = await dlcsClient.GetSpaceDetails(spaceId);
            if (space != null)
            {
                if (manifestMode)
                {
                    space.AddDefaultTag(SpaceX.ManifestTag);
                }
                else
                {
                    space.RemoveDefaultTag(SpaceX.ManifestTag);
                }
                await dlcsClient.PatchSpace(space);
            }
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
            var images = await dlcsClient.GetSpaceImages(page, pageSize, spaceId, nameof(Image.Number1));
            if (images.Members != null && images.Members.Any())
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
                await dlcsClient.PatchImages(images, spaceId);
            }

            return RedirectToPage("/spaces/details", new {id = spaceId});
        }
    }
}