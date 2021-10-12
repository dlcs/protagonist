using DLCS.HydraModel;
using DLCS.HydraModel.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DLCS.Mock.Controllers
{
    public class ContextsController : ControllerBase
    {
        private readonly HydraSettings settings;
            
        public ContextsController(IOptions<HydraSettings> options)
        {
            settings = options.Value;
        }
        
        [HttpGet]
        [Route("/contexts/{typeName}.jsonld")]
        public DlcsClassContext Index(string typeName)
        {
            return new(settings, typeName);
        }
    }
}