using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class DlcsApiController : ControllerBase
    {
        private readonly MockModel model;
        
        public DlcsApiController(
            MockModel model)
        {
            this.model = model;
        }
        
        [HttpGet]
        [Route("/")]
        public EntryPoint Index()
        {
            var ep = new EntryPoint();
            ep.Init(model.BaseUrl, true);
            return ep;
        }
        
    }
}