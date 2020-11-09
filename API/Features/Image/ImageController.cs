using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Features.Image.Commands;
using API.Features.Image.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Image
{
    [Route("/customers/{customerId}/spaces/{spaceId}/images/")]
    [ApiController]
    public class Image : Controller
    {
        private readonly IMediator mediator;

        public Image(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        /// <summary>
        /// Ingest specified file bytes to DLCS.
        /// </summary>
        [HttpPost]
        [Route("{imageId}")]
        public async Task<IActionResult> IngestBytes([FromRoute] string customerId, [FromRoute] string spaceId,
            [FromRoute] string imageId, AssetJsonLdWithBytes asset)
        {
            var claimsIdentity = User.Identity as ClaimsIdentity;
            var claim = claimsIdentity?.FindFirst("DlcsAuth").Value;
            var command = new IngestImageFromFile(customerId, spaceId, imageId,
                new MemoryStream(asset.File), asset.ToImageJsonLD(), claim);
            
            var response = await mediator.Send(command);

            HttpStatusCode? statusCode = response.Value?.DownstreamStatusCode ??
                                         (response.Success
                                             ? HttpStatusCode.Created
                                             : HttpStatusCode.InternalServerError);

            return StatusCode((int) statusCode, response.Value?.Body);
        }
    }
}