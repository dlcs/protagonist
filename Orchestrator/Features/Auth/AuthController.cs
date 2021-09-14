using Microsoft.AspNetCore.Mvc;

namespace Orchestrator.Features.Auth
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        /// <summary>
        /// Handle clickthrough auth request - create a new auth cookie and return View for user to close
        /// </summary>
        /// <param name="customer"></param>
        /// <returns></returns>
        [Route("{customer}/clickthrough")]
        [HttpGet]
        public IActionResult Clickthrough(int customer)
        {
            ViewData["Token"] = "test-token";
            return View();
        }
    }
}