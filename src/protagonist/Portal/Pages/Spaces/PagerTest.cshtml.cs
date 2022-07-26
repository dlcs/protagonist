using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.ViewComponents;

namespace Portal.Pages.Spaces;

public class PagerTest : PageModel
{
    public int Page { get; set; }
    public int Total { get; set; }
    public int PageSize { get; set; }
    public int Window { get; set; }
    public int Ends { get; set; }
    
    
    public void OnGet(
        [FromQuery] int page = 1, 
        [FromQuery] int total = 10000,
        [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize,
        [FromQuery] int window = PagerViewComponent.DefaultWindow,
        [FromQuery] int ends = PagerViewComponent.DefaultEnds)
    {
        
        Page = page;
        Total = total;
        PageSize = pageSize;
        Window = window;
        Ends = ends;
    }
}