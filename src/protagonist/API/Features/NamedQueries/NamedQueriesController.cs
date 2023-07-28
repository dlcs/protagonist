using System.Text.Json;
using API.Features.NamedQueries.Converters;
using API.Features.NamedQueries.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Collections;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using FluentValidation;
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostNamedQuery(
        [FromRoute] int customerId,
        [FromBody] NamedQuery newNamedQuery,
        [FromServices] HydraNamedQueryValidator validator,
        CancellationToken cancellationToken)
    {
        if (!IsGlobalValid(newNamedQuery)) 
            return this.HydraProblem("Only admins are allowed to create global Named Queries", null, 403);

        var validationResult = await validator.ValidateAsync(newNamedQuery,
            strategy => strategy.IncludeRuleSets("default", "create"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }

        newNamedQuery.CustomerId = customerId;
        var request = new CreateNamedQuery(customerId, newNamedQuery.ToDlcsModel());
        
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutNamedQuery(
        [FromRoute] int customerId,
        [FromRoute] string namedQueryId,
        [FromBody] NamedQuery namedQueryChanges,
        [FromServices] HydraNamedQueryValidator validator,
        CancellationToken cancellationToken)
    {
        if (!IsGlobalValid(namedQueryChanges)) 
            return this.HydraProblem("Only admins are allowed to create global named queries", null, 403);
        var validationResult = await validator.ValidateAsync(namedQueryChanges, 
            strategy => strategy.IncludeRuleSets("default", "update"), cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ValidationFailed(validationResult);
        }
        
        namedQueryChanges.ModelId = namedQueryId;
        var request = new UpdateNamedQuery(customerId, namedQueryChanges.ToDlcsModel());
        return await HandleUpsert(request, 
            nq => nq.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Failed to update named query",
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
    
    private bool IsGlobalValid(NamedQuery namedQuery)
        => !(namedQuery.Global == true && !User.IsAdmin());
}