using Microsoft.AspNetCore.Mvc;

namespace API.Features.Image
{
    [Route("/customers/{customerId}/spaces/{spaceId}/images/")]
    [ApiController]
    public class Image : Controller
    {
        [HttpPost]
        [Route("{imageId}")]
        public IActionResult Index([FromRoute]int customerId, [FromRoute]int spaceId, string imageId)
        {
            return Ok(new
            {
                customerId, spaceId, imageId
            });
        }
    }
}