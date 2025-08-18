using API.Features.NamedQueries.Requests;
using API.Infrastructure;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.NamedQueries;

/// <summary>
/// Controller for handling requests for customer resources
/// </summary>
[Route("/customers/{customerId}/resources")]
[ApiController]
public class CustomerResourcesController : HydraController
{
    /// <inheritdoc />
    public CustomerResourcesController(IOptions<ApiSettings> settings, IMediator mediator) 
        : base(settings.Value, mediator)
    {
    }
    
    /// <summary>
    /// Deletes PDF generated for queryName using specified arguments.
    /// This deletes both the control-file and PDF projection.
    /// </summary>
    /// <param name="customerId">CustomerId to delete PDF from</param>
    /// <param name="queryName">Name of named query</param>
    /// <param name="args">Named query arguments</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /customers/2/resources/pdf/my-named-query?args=1/10/teststring
    /// </remarks>
    [HttpDelete]
    [Route("pdf/{queryName}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePdf(
        [FromRoute] int customerId,
        [FromRoute] string queryName,
        [FromQuery] string args,
        CancellationToken cancellationToken = default)
    {
        const string errorTitle = "Delete PDF failed";
        return await HandleHydraRequest(async () =>
        {
            var deleteRequest = new DeletePdf(customerId, queryName, args);
            var result = await Mediator.Send(deleteRequest, cancellationToken);

            if (result == null)
            {
                return this.HydraProblem("Unable to parse named query request", null, 400, errorTitle);
            }

            // TODO - return a better message. This is for backwards compat
            return Ok(new { success = result });
        }, errorTitle);
    }
}