using System.IO;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace API.Features;

/// <summary>
/// 
/// </summary>
[Route("/")]
[ApiController]
public class HomeController : Controller
{
    private readonly IWebHostEnvironment hostingEnvironment;

    public HomeController(IWebHostEnvironment hostingEnvironment)
    {
        this.hostingEnvironment = hostingEnvironment;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    public EntryPoint Index()
    {
        return new(Request.GetBaseUrl());
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    [AllowAnonymous]
    [Route("/favicon.ico")]
    public IActionResult Favicon()
    {
        Response.CacheForDays(28);
        var icoPath = Path.Combine(hostingEnvironment.ContentRootPath, "favicon.ico");
        return PhysicalFile(icoPath, "image/x-icon");
    }
}