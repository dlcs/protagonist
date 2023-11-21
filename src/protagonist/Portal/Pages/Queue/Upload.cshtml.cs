using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages.Queue;

public class UploadModel : PageModel
{
    public int? SpaceId;
    
    public void OnGetAsync(
        [FromQuery] int? space)
    {
        SpaceId = space;
    }
}