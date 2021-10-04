using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.Users.Requests;

namespace Portal.Features.Users
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class UserController : Controller
    {
        private readonly IMediator mediator;

        public UserController(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        [HttpDelete]
        public async Task<IActionResult> DeleteAsync(string id)
        {
            var deleteSuccess = await mediator.Send(new DeletePortalUser(id));
            if (deleteSuccess)
            {
                return Ok(new {message = "Portal user '{id}' deleted"});
            }

            return StatusCode(500, new {message = "Error deleting portal user"});
        }
    }
}