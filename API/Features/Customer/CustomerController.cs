using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Customer.Requests;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
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
                // no paging hence no pageSize here; return all customers.
                Id = Request.GetDisplayUrl()
            };
        }

        public async Task<IActionResult> Create([FromBody] DLCS.HydraModel.Customer newCustomer)
        {
            // Where should this happen? Some happens in the controller, some in the Mediator handler.
            // Basic data checks (no DB access)
            if (newCustomer.ModelId > 0)
            {
                return BadRequest($"DLCS must allocate customer id, but id {newCustomer.ModelId} was supplied.");
            }

            if (string.IsNullOrEmpty(newCustomer.Name))
            {
                return BadRequest($"A new customer must have a name (url part).");
            }
            
            if (string.IsNullOrEmpty(newCustomer.DisplayName))
            {
                return BadRequest($"A new customer must have a Display name (label).");
            }
            
            if (newCustomer.Administrator == true)
            {
                return BadRequest($"You can't attempt to create an Administrator customer.");
            }

            if (newCustomer.Keys.HasText())
            {
                return BadRequest($"You can't supply API Keys at customer creation time.");
            }

            if (Enum.GetNames(typeof(DLCS.HydraModel.Customer.ReservedNames)).Any(n =>
                String.Equals(n, newCustomer.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                return BadRequest($"Name field cannot be a reserved word.");
            }

            var command = new CreateCustomer(newCustomer.Name, newCustomer.DisplayName);

            try
            {
                var newDbCustomer = await mediator.Send(command);
                var newApiCustomer = newDbCustomer.ToHydra(Request.GetBaseUrl());
                return Created(newApiCustomer.Id, newApiCustomer);
            }
            catch (BadRequestException badRequestException)
            {
                // Are exceptions the way this info should be passed back to the controller?
                return BadRequest(badRequestException.Message);
            }
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