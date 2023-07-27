using API.Features.NamedQueries.Converters;
using API.Features.NamedQueries.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.NamedQueries;

/// <summary>
/// DLCS REST API Operations for Named Queries
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
    
    /// <summary>
    /// Get a list of all available Named Queries, either global or owned by the user
    /// </summary>
    /// <returns>HydraCollection of NamedQuery</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNamedQueries(
        [FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var namedQueries = new GetAllNamedQueries(customerId);
        
        return await HandleListFetch<DLCS.Model.Assets.NamedQueries.NamedQuery, GetAllNamedQueries, NamedQuery>(
            namedQueries,
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to get Named Queries",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Create a new Named Query owned by the user - Only administrators may create a global named query
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST: /customers/1/namedQueries
    ///     {
    ///         "name":"my-named-query",
    ///         "template":"space=example"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostNamedQuery(
        [FromRoute] int customerId,
        [FromBody] NamedQuery newNamedQuery,
        [FromServices] HydraNamedQueryValidator validator,
        CancellationToken cancellationToken)
    {
        if (newNamedQuery.Global == true && !User.IsAdmin()) 
            return this.HydraProblem("Only admins are allowed to create global Named Queries", null, 400);
        
        var validationResult = await validator.ValidateAsync(newNamedQuery, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        var request = new CreateNamedQuery(customerId, newNamedQuery.ToDlcsModel(customerId));
        
        return await HandleUpsert(request, 
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create Named Query",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get a specified Named Query, either global or owned by the user
    /// </summary>
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
    
    /// <summary>
    /// Update an existing Named Query owned by the user - currently, only the template can be modified
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT: /customers/1/namedQueries/a90d6e44-4cdb-410b-999e-30c2ea3955b2
    ///     {
    ///         "template":"space=example-updated"
    ///     }
    /// </remarks>
    [HttpPut]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] string namedQueryId,
        [FromBody] NamedQuery newNamedQuery,
        [FromServices] HydraNamedQueryValidator validator,
        CancellationToken cancellationToken)
    {
        if (newNamedQuery.Global == true && !User.IsAdmin()) 
            return this.HydraProblem("Only admins are allowed to create global named queries", null, 400);

        if (!newNamedQuery.Name.IsNullOrEmpty())
            return this.HydraProblem("The name of a named query cannot be changed", null, 400);
        
        if (newNamedQuery.Template.IsNullOrEmpty())
            return this.HydraProblem("The template cannot be left blank", null, 400);

        newNamedQuery.ModelId = namedQueryId;
        var request = new UpdateNamedQuery(customerId, newNamedQuery.ToDlcsModel(customerId));
        
        return await HandleUpsert(request, 
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to create named query",
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Delete a specified Named Query owned by the user
    /// </summary>
    [HttpDelete]
    [Route("{namedQueryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] string namedQueryId)
    {
        var deleteRequest = new DeleteNamedQuery(customerId, namedQueryId);

        return await HandleDelete(deleteRequest);
    }
}