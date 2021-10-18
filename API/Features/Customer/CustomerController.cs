using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Customer.Requests;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace API.Features.Customer
{
    [Route("/customers/")]
    [ApiController]
    public class CustomerController : Controller
    {
        private readonly IMediator mediator;

        public CustomerController(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        [AllowAnonymous]
        [HttpGet]
        public async Task<HydraCollection<JObject>> Index()
        {
            var baseUrl = Request.GetBaseUrl();
            var dbCustomers = await mediator.Send(new GetAllCustomers());
            
            return new HydraCollection<JObject>
            {
                IncludeContext = true,
                Members = dbCustomers.Select(c => c.ToCollectionForm(baseUrl)).ToArray(),
                TotalItems = dbCustomers.Count,
                Id = Request.GetDisplayUrl()
            };
        }
        
        
        [HttpGet]
        [Route("{customerId}")]
        public async Task<DLCS.HydraModel.Customer> Index(int customerId)
        {
            var baseUrl = Request.GetBaseUrl();
            var dbCustomer = await mediator.Send(new GetCustomer(customerId));
            return dbCustomer.ToHydra(baseUrl);
        }
    }
}