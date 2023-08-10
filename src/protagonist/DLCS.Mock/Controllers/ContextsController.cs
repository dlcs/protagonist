using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers;

public class ContextsController : ControllerBase
{
    private readonly MockModel model;
        
    public ContextsController(MockModel model)
    {
        this.model = model;
    }
    
    [HttpGet]
    [Route("/contexts/{typeName}.jsonld")]
    public DlcsClassContext Index(string typeName)
    {
        return new(model.BaseUrl, "DLCS.HydraModel." + typeName);
    }
}