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
[Route("/customers/{customer}/namedQueries")]
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
    public async Task<IActionResult> GetNamedQueries()
    {
        throw new NotImplementedException();
    }
    
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostNamedQuery()
    { 
        throw new NotImplementedException();
    }
    
    [HttpGet]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNamedQuery(
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
        [FromRoute] int namedQueryId)
    {
        throw new NotImplementedException();
    }
    
    [HttpDelete]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status205ResetContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNamedQuery(
        [FromRoute] int namedQueryId)
    {
        throw new NotImplementedException();
    }
}