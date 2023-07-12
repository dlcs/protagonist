using API.Infrastructure;
using API.Settings;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.NamedQueries;

/// <summary>
/// Controller for handling Named Queries
/// </summary>
[Route("/customers/{customerId}/namedQueries")]
[ApiController]
public class NamedQueriesController : HydraController
{
    public NamedQueriesController(
        IMediator mediator,
        IOptions<ApiSettings> options) : base(options.Value, mediator)
    {
    }
    
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNamedQueries(
        [FromRoute] int customerId)
    {
        throw new NotImplementedException();
    }
    
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostNamedQuery(
        [FromRoute] int customerId)
    { 
        throw new NotImplementedException();
    }
    
    [HttpGet]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] int namedQueryId)
    {
        throw new NotImplementedException();
    }
    
    [HttpPut]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status205ResetContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] int namedQueryId)
    {
        throw new NotImplementedException();
    }
    
    [HttpDelete]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status205ResetContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] int namedQueryId)
    {
        throw new NotImplementedException();
    }
}