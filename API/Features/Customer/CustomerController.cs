using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Converters;
using API.Features.Customer.Requests;
using API.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Model.Customers;
using DLCS.Web.Auth;
using DLCS.Web.Requests;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace API.Features.Customer
{
    /// <summary>
    /// DLCS REST API Operations for customers.
    /// This controller does not do any data access; it creates Mediatr requests and passes them on.
    /// It converts to and from the Hydra form of the DLCS API.
    /// </summary>
    [Route("/customers/")]
    [ApiController]
    public class CustomerController : HydraController
    {
        private readonly IMediator mediator;

        /// <inheritdoc />
        public CustomerController(
            IMediator mediator,
            IOptions<ApiSettings> options) : base(options.Value)
        {
            this.mediator = mediator;
        }
        
        // ################# GET /customers #####################
        /// <summary>
        /// Get all the customers.
        /// Although it returns a paged collection, the page size is always the total number of customers:
        /// clients don't need to page this collection, it contains all customers.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet]
        public async Task<HydraCollection<JObject>> Index()
        {
            var baseUrl = getUrlRoots().BaseUrl;
            var dbCustomers = await mediator.Send(new GetAllCustomers());
            
            return new HydraCollection<JObject>
            {
                WithContext = true,
                Members = dbCustomers.Select(c => c.ToCollectionForm(baseUrl)).ToArray(),
                TotalItems = dbCustomers.Count,
                PageSize = dbCustomers.Count,
                Id = Request.GetJsonLdId()
            };
        }

        
        // ################# POST /customers #####################
        /// <summary>
        /// The /customers/ path is not access controlled, but only an admin may call this.
        /// </summary>
        /// <param name="newCustomer"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DLCS.HydraModel.Customer newCustomer)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }
            
            var basicErrors = HydraCustomerValidator.GetNewHydraCustomerErrors(newCustomer);
            if (basicErrors.Any())
            {
                return HydraProblem(basicErrors, null, 400, "Invalid Customer", null);
            }

            var command = new CreateCustomer(newCustomer.Name!, newCustomer.DisplayName!);

            try
            {
                var result = await mediator.Send(command);
                if (result.Customer == null || result.ErrorMessages.Any())
                {
                    int statusCode = result.Conflict ? 409 : 500;
                    return HydraProblem(result.ErrorMessages, null, statusCode, "Could not create Customer", null);
                }
                var newApiCustomer = result.Customer.ToHydra(getUrlRoots().BaseUrl);
                return Created(newApiCustomer.Id, newApiCustomer);
            }
            catch (Exception ex)
            {
                // Are exceptions the way this info should be passed back to the controller?
                return HydraProblem(ex);
            }
        }
        
        
        // ################# GET /customers/id #####################
        /// <summary>
        /// Get a Customer
        /// </summary>
        /// <param name="customerId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{customerId}")]
        public async Task<IActionResult> Index(int customerId)
        {
            var dbCustomer = await mediator.Send(new GetCustomer(customerId));
            if (dbCustomer == null)
            {
                return HydraNotFound();
            }
            return Ok(dbCustomer.ToHydra(getUrlRoots().BaseUrl));
        }
        
        
        // ################# GET /customers/id/keys #####################
        [HttpGet]
        [Route("{customerId}/keys")]
        public async Task<IActionResult> GetApiKeys(int customerId)
        {
            var dbCustomer = await mediator.Send(new GetCustomer(customerId));
            if (dbCustomer == null)
            {
                return HydraNotFound();
            }

            var urlRoots = getUrlRoots();
            var collection = new HydraCollection<ApiKey>
            {
                WithContext = true,
                Members = dbCustomer.Keys.Select(
                    key => new ApiKey(urlRoots.BaseUrl, customerId, key, null))
                    .ToArray(),
                TotalItems = dbCustomer.Keys.Length,
                PageSize = dbCustomer.Keys.Length,
                Id = Request.GetJsonLdId()
            };
            return Ok(collection);
        }
        
        
        // ################# POST /customers/id/keys #####################
        [HttpPost]
        [Route("{customerId}/keys")]
        public async Task<IActionResult> CreateNewApiKey(int customerId)
        {
            var result = await mediator.Send(new CreateApiKey(customerId));
            if (result.Key.HasText() && result.Secret.HasText())
            {
                return Ok(new ApiKey(getUrlRoots().BaseUrl, customerId, result.Key, result.Secret));
            }

            return HydraProblem("Unable to create API key", null, 500, "API Key", null);
        }
        
        
        // ################# DELETE /customers/id/keys/key #####################
        [HttpDelete]
        [Route("{customerId}/keys/{key}")]
        public async Task<IActionResult> DeleteKey(int customerId, string key)
        {
            var result = await mediator.Send(new DeleteApiKey(customerId, key));
            if (result.Error.HasText())
            {
                return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request", null);
            }

            return NoContent();
        }
        
        
        
        // ################# GET /customers/id/portalUsers #####################
        [HttpGet]
        [Route("{customerId}/portalUsers")]
        public async Task<HydraCollection<PortalUser>> GetUsers(int customerId)
        {
            var users = await mediator.Send(new GetPortalUsers { CustomerId = customerId });
            
            var baseUrl = getUrlRoots().BaseUrl;
            var collection = new HydraCollection<DLCS.HydraModel.PortalUser>
            {
                WithContext = true,
                Members = users.Select(s => s.ToHydra(baseUrl)).ToArray(),
                TotalItems = users.Count,
                PageSize = users.Count,
                Id = Request.GetJsonLdId()
            };
            return collection;
        }
        
        // ################# GET /customers/id/portalUsers/id #####################
        [HttpGet]
        [Route("{customerId}/portalUsers/{userId}")]
        public async Task<IActionResult> GetUser(int customerId, string userId)
        {
            var users = await mediator.Send(new GetPortalUsers { CustomerId = customerId });
            var user = users.SingleOrDefault(u => u.Id == userId);
            if (user != null)
            {
                return Ok(user.ToHydra(getUrlRoots().BaseUrl));
            }

            return HydraNotFound();
        }
        
        
        // ################# POST /customers/id/portalUsers #####################
        [HttpPost]
        [Route("{customerId}/portalUsers")]
        public async Task<IActionResult> CreateUser(int customerId, [FromBody] PortalUser portalUser)
        {
            var request = new CreatePortalUser
            {
                Password = portalUser.Password,
                PortalUser = new User
                {
                    Customer = customerId,
                    Email = portalUser.Email
                }
            };
            var result = await mediator.Send(request);
            if (result.Error.HasText() || result.PortalUser == null)
            {
                return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Cannot create user", null);
            }
            
            var hydraPortalUser = result.PortalUser.ToHydra(getUrlRoots().BaseUrl);
            return Created(hydraPortalUser.Id, hydraPortalUser);
        }
        
        
        // ################# PATCH /customers/id/portalUsers/id #####################
        [HttpPatch]
        [Route("{customerId}/portalUsers/{userId}")]
        public async Task<IActionResult> PatchUser(int customerId, string userId, [FromBody] PortalUser portalUser)
        {
            // NB Deliverator doesn't support toggling Enabled here so we won't for now.
            
            var request = new PatchPortalUser
            {
                Password = portalUser.Password,
                PortalUser = new User
                {
                    Id = userId,
                    Customer = customerId,
                    Email = portalUser.Email
                }
            };
            var result = await mediator.Send(request);
            if (result.Error.HasText() || result.PortalUser == null)
            {
                return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Cannot Patch user", null);
            }
            
            var hydraPortalUser = result.PortalUser.ToHydra(getUrlRoots().BaseUrl);
            return Ok(hydraPortalUser);
        }
        
        
        // ################# DELETE /customers/id/portalUsers/id #####################
        [HttpDelete]
        [Route("{customerId}/portalUsers/{userId}")]
        public async Task<IActionResult> DeleteUser(int customerId, string userId)
        {
            var result = await mediator.Send(new DeletePortalUser(customerId, userId));
            if (result.Error.HasText())
            {
                return HydraProblem(result.Error, null, (int)HttpStatusCode.BadRequest, "Bad Request", null);
            }

            return NoContent();
        }
    }

}