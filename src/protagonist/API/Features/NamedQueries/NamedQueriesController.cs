using API.Features.NamedQueries.Converters;
using API.Features.NamedQueries.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
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
        [FromBody] NamedQuery newNamedQuery,
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var request = new CreateOrUpdateNamedQuery(customerId, newNamedQuery.ToDlcsModel(customerId), Request.Method);
        
        return await HandleUpsert(request, 
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create Named Query",
            cancellationToken: cancellationToken);
    }

    [HttpGet]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] string namedQueryId,
        CancellationToken cancellationToken)
    {
        var namedQuery = new GetNamedQuery(customerId, namedQueryId);
        
        return await HandleFetch(
            namedQuery,
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get Named Query",
            cancellationToken: cancellationToken
        );
    }
    
    [HttpPut]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status205ResetContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] string namedQueryId,
        [FromBody] NamedQuery newNamedQuery,
        CancellationToken cancellationToken)
    {
        newNamedQuery.Id = namedQueryId;
        var request = new CreateOrUpdateNamedQuery(customerId, newNamedQuery.ToDlcsModel(customerId), Request.Method);
        
        return await HandleUpsert(request, 
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update/create Named Query",
            cancellationToken: cancellationToken);
    }
    
    [HttpDelete]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status205ResetContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] int namedQueryId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}