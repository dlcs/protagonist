using System.Security.Claims;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using IIIF;
using IIIF.ImageApi.V3;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Images;

[BindProperties]
public class Index : PageModel
{
    private readonly IMediator mediator;
    private readonly DlcsSettings dlcsSettings;
    public Image Image { get; set; }
    public ImageService3? ImageThumbnailService { get; set; }
    public ImageStorage? ImageStorage { get; set; }
    public string SingleAssetManifest { get; set; }
    public string UniversalViewerManifest  { get; set; }
    public string Customer { get; set; }
    
    public Index(IMediator mediator, ClaimsPrincipal currentUser, IOptions<DlcsSettings> dlcsSettings)
    {
        this.mediator = mediator;
        this.dlcsSettings = dlcsSettings.Value;
        Customer = (currentUser.GetCustomerId() ?? -1).ToString();
    }
    
    public async Task<IActionResult> OnGetAsync(int space, string image)
    {
        var imageResult = await mediator.Send(new GetImage{SpaceId = space, ImageId = image});
        if (imageResult == null)
        {
            TempData[PageConstants.TempErrorMessageKey] = "The requested image was not found";
            return NotFound();
        }
        
        Image = imageResult.Image;
        ImageThumbnailService = imageResult.ImageThumbnailService;
        ImageStorage = imageResult.ImageStorage;
        SingleAssetManifest = DlcsPathHelpers.GeneratePathFromTemplate(
            dlcsSettings.SingleAssetManifestTemplate,
            prefix: dlcsSettings.ResourceRoot.ToString(),
            customer: Customer,
            space: Image.Space.ToString(),
            assetPath: Image.ModelId);
        UniversalViewerManifest = CreateUniversalViewerUrl(SingleAssetManifest);
        return Page();
    }
    
    public string CreateSrc(Size size)
    {
        return $"{Image.ThumbnailImageService}/full/{size.Width},{size.Height}/0/default.jpg";
    }
    
    public string CreateUniversalViewerUrl(string singleAssetManifest)
    {
        return $"https://universalviewer.io/?manifest={singleAssetManifest}"; 
    }
}