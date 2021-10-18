using System.Linq;
using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class ImageOptimisationPoliciesController : ControllerBase
    {
        private readonly MockModel model;
        
        public ImageOptimisationPoliciesController(MockModel model)
        {
            this.model = model;
        }
        
        [HttpGet]
        [Route("/imageOptimisationPolicies")]
        public HydraCollection<ImageOptimisationPolicy> Index()
        {
            var imageOptimisationPolicies = model.ImageOptimisationPolicies.ToArray();

            return new HydraCollection<ImageOptimisationPolicy>
            {
                IncludeContext = true,
                Members = imageOptimisationPolicies,
                TotalItems = imageOptimisationPolicies.Length,
                Id = Request.GetDisplayUrl()
            };
        }


        [HttpGet]
        [Route("/imageOptimisationPolicies/{id}")]
        public IActionResult Index(string id)
        {
            var iop = model.ImageOptimisationPolicies.SingleOrDefault(
                p => p.ModelId == id);
            if (iop != null)
            {
                return Ok(iop);
            }
            return NotFound();
        }
        
    }
}