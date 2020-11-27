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
        /// "File" property should be base64 encoded image. 
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT: /customers/1/spaces/1/images/my-image
        ///     {
        ///         "@type":"Image",
        ///         "family": "I",
        ///         "file": "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAM...."
        ///     }
        /// </remarks>
        [ProducesResponseType(201, Type = typeof(AssetJsonLD))]
        [ProducesResponseType(400, Type = typeof(ProblemDetails))]
        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000, ValueLengthLimit = 100_000_000)]
        [Route("{imageId}")]
        public async Task<IActionResult> IngestBytes([FromRoute] string customerId, [FromRoute] string spaceId,
            [FromRoute] string imageId, [FromBody] AssetJsonLdWithBytes asset)
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