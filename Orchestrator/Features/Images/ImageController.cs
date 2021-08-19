using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Orchestrator.Features.Images.Requests;

namespace Orchestrator.Features.Images
{
    [Route("iiif-img/{customer}/{space}/{image}")]
    [ApiController]
    public class ImageController : Controller
    {
        private readonly IMediator mediator;
        private readonly ILogger<ImageController> logger;

        public ImageController(IMediator mediator, ILogger<ImageController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }
        
        /// <summary>
        /// Index request for image root, redirects to info.json.
        /// </summary>
        /// <returns></returns>
        [Route("", Name = "image_only")]
        [HttpGet]
        public RedirectResult Index()
            => Redirect(HttpContext.Request.Path.Add("/info.json"));

        /// <summary>
        /// Get info.json file for specified image
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [Route("info.json", Name = "info_json")]
        [HttpGet]
        public async Task<IActionResult> InfoJson(CancellationToken cancellationToken)
        {
            try
            {

                var infoJsonResponse = await mediator.Send(new GetImageInfoJson(HttpContext.Request.Path), cancellationToken);
                if (!infoJsonResponse.HasInfoJson) return NotFound();

                if (infoJsonResponse.RequiresAuth)
                {
                    
                    Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                }
                
                HttpContext.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };
                return Content(infoJsonResponse.InfoJson, "application/json");
            }
            catch (KeyNotFoundException ex)
            {
                // TODO - this error handling duplicates same in RequestHandlerBase
                logger.LogError(ex, "Could not find Customer/Space from '{Path}'", HttpContext.Request.Path);
                return NotFound();
            }
            catch (FormatException ex)
            {
                logger.LogError(ex, "Error parsing path '{Path}'", HttpContext.Request.Path);
                return BadRequest();
            }
        }
        
        /*private void SetCacheControl(bool requiresAuth)
        {
            if (cacheSeconds > 0)
            {
                HttpContext.Response.GetTypedHeaders().CacheControl =
                    new CacheControlHeaderValue
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(cacheSeconds)
                    };
            }
        }*/
    }
}