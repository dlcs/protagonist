using System.Linq;
using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers;

[ApiController]
public class OriginStrategiesController : ControllerBase
{
    private readonly MockModel model;
    
    public OriginStrategiesController(MockModel model)
    {
        this.model = model;
    }
    
    [HttpGet]
    [Route("/originStrategies")]
    public HydraCollection<OriginStrategy> Index()
    {
        var originStrategies = model.OriginStrategies.ToArray();

        return new HydraCollection<OriginStrategy>
        {
            WithContext = true,
            Members = originStrategies,
            TotalItems = originStrategies.Length,
            Id = Request.GetDisplayUrl()
        };
    }


    [HttpGet]
    [Route("/originStrategies/{id}")]
    public IActionResult Index(string id)
    {
        var originStrategy = model.OriginStrategies.SingleOrDefault(os => os.ModelId == id);
        if (originStrategy != null)
        {
            return Ok(originStrategy);
        }
        return NotFound();
    }
}