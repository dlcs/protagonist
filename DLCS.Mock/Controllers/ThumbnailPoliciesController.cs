using System.Linq;
using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class ThumbnailPoliciesController : ControllerBase
    {        
        private readonly MockModel model;
        
        public ThumbnailPoliciesController(MockModel model)
        {
            this.model = model;
        }
        
        [HttpGet]
        [Route("/thumbnailPolicies")]
        public HydraCollection<ThumbnailPolicy> Index()
        {
            var thumbnailPolicies = model.ThumbnailPolicies.ToArray();

            return new HydraCollection<ThumbnailPolicy>
            {
                IncludeContext = true,
                Members = thumbnailPolicies,
                TotalItems = thumbnailPolicies.Length,
                Id = Request.GetDisplayUrl()
            };
        }


        [HttpGet]
        [Route("/thumbnailPolicies/{id}")]
        public IActionResult Index(string id)
        {
            var tp = model.ThumbnailPolicies.SingleOrDefault(p => p.ModelId == id);
            if (tp != null)
            {
                return Ok(tp);
            }
            return NotFound();
        }
    }
}