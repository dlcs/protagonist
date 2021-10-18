using System.IO;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace API.Features
{
    /// <summary>
    /// 
    /// </summary>
    [Route("/")]
    [ApiController]
    public class HomeController : Controller
    {
        private IWebHostEnvironment hostingEnvironment;

        public HomeController(IWebHostEnvironment hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;
        }
        
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public EntryPoint Index()
        {
            return new(Request.GetBaseUrl());
        }
        
        
        [AllowAnonymous]
        [Route("/favicon.ico")]
        public IActionResult Favicon()
        {
            Response.CacheForDays(28);
            var icoPath = Path.Combine(hostingEnvironment.ContentRootPath, "favicon.ico");
            return PhysicalFile(icoPath, "image/x-icon");
        }
        
        [Route("/{name}")]
        public IActionResult Demo(string name)
        {
            return Json("This is a non-customer resource that can only be called by admin");
        }
    }
}