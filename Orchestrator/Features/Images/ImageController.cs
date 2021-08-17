using Microsoft.AspNetCore.Mvc;

namespace Orchestrator.Features.Images
{
    [Route("iiif-img/{customer}/{space}/{image}")]
    [ApiController]
    public class ImageController : Controller
    {
        /// <summary>
        /// Index request for image root, redirects to info.json.
        /// </summary>
        /// <returns></returns>
        [Route("", Name = "image_only")]
        public RedirectResult Index()
            => Redirect(HttpContext.Request.Path.Add("/info.json"));
    }
}