using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features
{
    [Route("/")]
    [ApiController]
    public class HomeController : Controller
    {
        [AllowAnonymous]
        public IActionResult Index()
        {
            return Json("This is API home. ");
        }
        
        [Route("/{name}")]
        public IActionResult Demo(string name)
        {
            return Json("This is a non-customer resource that can only be called by admin");
        }
    }
}