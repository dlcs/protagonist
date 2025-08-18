using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core;
using DLCS.Core.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Models;
using Portal.Settings;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces;

[BindProperties]
public class Details : PageModel
{
    private readonly IDlcsClient dlcsClient;
    private readonly ILogger<Details> logger;
    public DlcsSettings DlcsSettings { get; }
    private readonly PortalSettings portalSettings;
    public SpacePageModel SpacePageModel { get; set; }
    public PagerValues? PagerValues { get; private set; }
    public string Customer { get; set; }
    public int SpaceId { get; set; }

    public Details(
        IDlcsClient dlcsClient,
        ILogger<Details> logger,
        IOptions<DlcsSettings> dlcsSettings,
        IOptions<PortalSettings> portalSettings,
        ClaimsPrincipal currentUser)
    {
        this.dlcsClient = dlcsClient;
        this.logger = logger;
        DlcsSettings = dlcsSettings.Value;
        this.portalSettings = portalSettings.Value;
        Customer = (currentUser.GetCustomerId() ?? -1).ToString();
    }
    
    public async Task<IActionResult> OnGetAsync(int id, 
        [FromQuery] int page = 1, [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize,
        [FromQuery] string? orderBy = null, [FromQuery] string? orderByDescending = null)
    {
        var descending = false;
        if (orderByDescending.HasText())
        {
            orderBy = orderByDescending;
            descending = true;
        }
        
        SpaceId = id;

        Space? space;
        try
        {
            space = await dlcsClient.GetSpaceDetails(SpaceId);
        }
        catch(DlcsException ex)
        {
            logger.LogError(ex, "Failed to retrieve space {CustomerId}/{SpaceId} from API", Customer, SpaceId);
            TempData[PageConstants.TempErrorMessageKey] = "The requested space was not found";
            return NotFound();
        }
     
        var storage = await dlcsClient.GetSpaceStorage(SpaceId);
        var images = await dlcsClient.GetSpaceImages(page, pageSize, SpaceId, 
            orderBy ?? nameof(Image.Number1), descending);
        var model = new SpacePageModel
        {
            Space = space,
            Images = images,
            Storage = storage,
            IsManifestSpace = space?.IsManifestSpace() ?? false
        };

        if (model.IsManifestSpace)
        {
            SetManifestLinks(model);
        }

        SpacePageModel = model;
        SetPager(page, pageSize, orderBy, descending);
        return Page();
    }

    private void SetPager(int page, int pageSize, string? orderBy, bool descending)
    {
        var partialImageCollection = SpacePageModel.Images;
        PagerValues = new PagerValues(
            partialImageCollection?.TotalItems ?? 0,
            page,
            partialImageCollection?.PageSize ?? pageSize,
            orderBy, descending);
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
    
    /// <summary>
    /// Toggle space between manifest mode and normal mode by adding/removing special tag
    /// </summary>
    /// <param name="spaceId"></param>
    /// <returns></returns>
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
            try
            {
                await dlcsClient.PatchSpace(spaceId, space);
            }
            catch (DlcsException dlcsException)
            {
                TempData["error-message"] = dlcsException.Message;
            }
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

    public string GetSizeInGb(long? bytes)
    {
        if (bytes.HasValue)
        {
            return $"{(((Convert.ToDouble(bytes.Value) / 1024) / 1024) / 1024):0.00}";  
        }

        return "N/A";
    }
}