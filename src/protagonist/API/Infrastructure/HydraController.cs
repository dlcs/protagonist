using System;
using System.Threading;
using System.Threading.Tasks;
using API.Converters;
using API.Exceptions;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Infrastructure;

/// <summary>
/// Base class for DLCS API Controllers that return Hydra responses
/// </summary>
public abstract class HydraController : Controller
{
    /// <summary>
    /// API Settings available to derived controller classes
    /// </summary>
    protected readonly ApiSettings Settings;

    protected readonly IMediator mediator;

    /// <inheritdoc />
    protected HydraController(ApiSettings settings, IMediator mediator)
    {
        Settings = settings;
        this.mediator = mediator;
    }

    /// <summary>
    /// Used by derived controllers to construct correct fully qualified URLs in returned Hydra objects.
    /// </summary>
    /// <returns></returns>
    protected UrlRoots GetUrlRoots()
    {
        return new UrlRoots
        {
            BaseUrl = Request.GetBaseUrl(),
            ResourceRoot = Settings.DLCS.ResourceRoot.ToString()
        };
    }
    
    /// <summary>
    /// Handle an upsert request - this takes a IRequest which returns a ModifyEntityResult{T}.
    /// The request is sent and result is transformed to an http hydra result.  
    /// </summary>
    /// <param name="request">IRequest to modify data</param>
    /// <param name="hydraBuilder">Delegate to transform returned entity to a Hydra representation</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="errorTitle">
    /// The value for <see cref="Error.Title" />. In some instances this will be prepended to the actual error name.
    /// e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <returns>
    /// ActionResult generated from ModifyEntityResult. This will be the Hydra model + 200/201 on success. Or a Hydra
    /// error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandleUpsert<T>(
        IRequest<ModifyEntityResult<T>> request,
        Func<T, DlcsResource> hydraBuilder,
        string? instance = null,
        string? errorTitle = "Operation failed",
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var result = await mediator.Send(request, cancellationToken);

            return this.ModifyResultToHttpResult(result, hydraBuilder, instance, errorTitle);
        }
        catch (APIException apiEx)
        {
            return this.HydraProblem(apiEx.Message, null, apiEx.StatusCode ?? 500, apiEx.Label);
        }
        catch (Exception ex)
        {
            return this.HydraProblem(ex.Message, null, 500, errorTitle);
        }
    }
}