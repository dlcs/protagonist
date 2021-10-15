using System.Linq;
using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class PortalRolesController : ControllerBase
    {
        private readonly MockModel model;
        
        public PortalRolesController(MockModel model)
        {
            this.model = model;
        }
        
        [HttpGet]
        [Route("/portalRoles")]
        public HydraCollection<PortalRole> Index()
        {
            var portalRoles = model.PortalRoles.ToArray();

            return new HydraCollection<PortalRole>
            {
                IncludeContext = true,
                Members = portalRoles,
                TotalItems = portalRoles.Length,
                Id = Request.GetDisplayUrl()
            };
        }

        [HttpGet]
        [Route("/portalRoles/{id}")]
        public IActionResult Index(string id)
        {
            var portalRole = model.PortalRoles.SingleOrDefault(pr => pr.ModelId == id);
            if (portalRole != null)
            {
                return Ok(portalRole);
            }
            return NotFound();
        }

        [HttpGet]
        [Route("/customers/{customerId}/portalUsers/{portalUserId}/roles")]
        public HydraCollection<PortalRole> RolesForUser(int customerId, string portalUserId)
        {
            var userid = Request.GetDisplayUrl().Replace("/roles", "");
            // need to make this use last part.,,
            var roleIdsForUser = model.PortalUserRoles[userid];
            var roles = model.PortalRoles.Where(pr => roleIdsForUser.Contains(pr.Id)).ToArray();

            return new HydraCollection<PortalRole>
            {
                IncludeContext = true,
                Members = roles,
                TotalItems = roles.Length,
                Id = Request.GetDisplayUrl()
            };
        }
    }
}