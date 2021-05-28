using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Portal.TagHelpers
{
    public class NavlinkTagHelper : TagHelper
    {
        public string Href { get; set; }
        public string Icon { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var content = (await output.GetChildContentAsync()).GetContent();
            output.TagName = "a";
            var pagePath = ViewContext.HttpContext.Request.Path;
            if (Href == pagePath || Href != "/" && pagePath.StartsWithSegments(Href))
            {
                output.Attributes.Add("class", "nav-link active");
                output.Attributes.Add("aria-current", "page");
            }
            else
            {
                output.Attributes.Add("class", "nav-link");
            }

            output.Attributes.Add("href", Href);
            output.Content.SetHtmlContent("<span data-feather=\"" + Icon + "\"></span> " + content);
        }

        [ViewContext] public ViewContext ViewContext { get; set; }
    }
}
