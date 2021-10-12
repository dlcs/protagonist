using Microsoft.AspNetCore.Mvc;

namespace API.Features.TestResource
{
    [Route("/customers/{customerId}/test/")]
    public class TestResourceController : Controller
    {
        [Route("{resourceName}")]
        public IActionResult TestResource(string resourceName)
        {
            return Json("A test resource '" + resourceName + "' that can be accessed by its owner customer, or admin.");
        }
    }
}