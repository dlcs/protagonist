using System.Collections.Generic;
using API.Converters;
using API.Exceptions;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Core;
using DLCS.HydraModel;
using DLCS.Model.Page;
using DLCS.Web.Requests;
using Hydra.Collections;
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

    protected readonly IMediator Mediator;

    /// <inheritdoc />
    protected HydraController(ApiSettings settings, IMediator mediator)
    {
        Settings = settings;
        Mediator = mediator;
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
        return await HandleHydraRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return this.ModifyResultToHttpResult(result, hydraBuilder, instance, errorTitle);
        }, errorTitle);
    }

    /// <summary>
    /// Handles a deletion
    /// </summary>
    /// <param name="request">The request/response to be sent through Mediatr</param>
    /// <param name="errorTitle">The title of the error</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="DeleteResult"/> is not understood</exception>
    /// ActionResult generated from DeleteResult. This will be 204 on success. Or a Hydra
    /// error and appropriate status code if failed.
    protected async Task<IActionResult> HandleDelete(
        IRequest<ResultMessage<DeleteResult>> request,
        string? errorTitle = "Delete failed",
        CancellationToken cancellationToken = default)
    {
        return await HandleHydraRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return result.Value switch
            {
                DeleteResult.NotFound => this.HydraNotFound(),
                DeleteResult.Conflict => this.HydraProblem(result.Message, null, 409,
                    "Delete failed"),
                DeleteResult.Error => this.HydraProblem(result.Message, null, 500,
                    "Error when deleting"),
                DeleteResult.Deleted => NoContent(),
                _ => throw new ArgumentOutOfRangeException(nameof(DeleteResult),$"No deletion value of {result.Value}")
            };
        }, errorTitle);
    }
    
    /// <summary>
    /// Handle a GET request - this takes a IRequest which returns a FetchEntityResult{T}.
    /// The request is sent and result is transformed to an http hydra result.  
    /// </summary>
    /// <param name="request">IRequest to fetch data</param>
    /// <param name="hydraBuilder">Delegate to transform returned entity to a Hydra representation</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="errorTitle">
    /// The value for <see cref="Error.Title" />. In some instances this will be prepended to the actual error name.
    /// e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="T">Type of entity being fetched</typeparam>
    /// <returns>
    /// ActionResult generated from FetchEntityResult. This will be the Hydra model + 200 on success. Or a Hydra
    /// error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandleFetch<T>(
        IRequest<FetchEntityResult<T>> request,
        Func<T, DlcsResource> hydraBuilder,
        string? instance = null,
        string? errorTitle = "Fetch failed",
        CancellationToken cancellationToken = default)
        where T : class
    {
        return await HandleHydraRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return this.FetchResultToHttpResult(result, instance, errorTitle, hydraBuilder);
        }, errorTitle);
    }

    /// <summary>
    /// Handle a GET request that returns a page of assets.
    /// This takes a IRequest which returns a FetchEntityResult{PageOf{T}} and inherits from IPagedRequest
    /// Prior to making request the Page and PageSize properties are set from query parameters.
    /// The request is sent and result is transformed to HydraCollection.  
    /// </summary>
    /// <param name="request">IRequest to fetch data</param>
    /// <param name="hydraBuilder">Delegate to transform each returned entity to a Hydra representation</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="errorTitle">
    /// The value for <see cref="Error.Title" />. In some instances this will be prepended to the actual error name.
    /// e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="TEntity">Type of db entity being fetched</typeparam>
    /// <typeparam name="TRequest">Type of mediatr request being page</typeparam>
    /// <typeparam name="THydra">Hydra type for each member</typeparam>
    /// <returns>
    /// ActionResult generated from FetchEntityResult. This will be the HydraCollection + 200 on success. Or a Hydra
    /// error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandlePagedFetch<TEntity, TRequest, THydra>(
        TRequest request,
        Func<TEntity, THydra> hydraBuilder,
        string? instance = null,
        string? errorTitle = "Fetch failed",
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TRequest : IRequest<FetchEntityResult<PageOf<TEntity>>>, IPagedRequest
        where THydra : DlcsResource
    {
        return await HandleHydraRequest(async () =>
        {
            SetPaging(request);
            if (request is IOrderableRequest orderableRequest)
            {
                SetOrderBy(orderableRequest);
            }

            var result = await Mediator.Send(request, cancellationToken);

            return this.FetchResultToHttpResult(
                result,
                instance,
                errorTitle, pageOf =>
                {
                    var collection = new HydraCollection<THydra>
                    {
                        WithContext = true,
                        Members = pageOf.Entities.Select(b => hydraBuilder(b)).ToArray(),
                        TotalItems = pageOf.Total,
                        PageSize = pageOf.PageSize,
                        Id = Request.GetJsonLdId()
                    };
                    PartialCollectionView.AddPaging(collection, new PartialCollectionViewPagingValues
                    {
                        Page = pageOf.Page, PageSize = pageOf.PageSize,
                        FurtherParameters = GetFurtherPageLinkParameters(request)
                    });
                    return collection;
                });
        }, errorTitle);
    }

    private List<KeyValuePair<string, string>>? GetFurtherPageLinkParameters(IPagedRequest pagedRequest)
    {
        List<KeyValuePair<string, string>>? furtherParameters = null;
        
        if (pagedRequest is IAssetFilterableRequest assetFilterableRequest)
        {
            if (assetFilterableRequest.AssetFilter != null)
            {
                var imageQuery = assetFilterableRequest.AssetFilter.ToImageQuery();
                furtherParameters ??= new List<KeyValuePair<string, string>>();
                furtherParameters.Add(new KeyValuePair<string, string>("q", imageQuery.ToQueryParam()));
            }
        }
        
        // Add any other parameters we want to pass through here
        
        return furtherParameters;
    }

    /// <summary>
    /// Handle a request that returns a non-paged list of assets.
    /// This takes a IRequest which returns a FetchEntityResult{IReadOnlyCollection{T}}
    /// The request is sent and result is transformed to HydraCollection.
    /// </summary>
    /// <param name="request">IRequest to fetch data</param>
    /// <param name="hydraBuilder">Delegate to transform each returned entity to a Hydra representation</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="errorTitle">
    /// The value for <see cref="Error.Title" />. In some instances this will be prepended to the actual error name.
    /// e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="TEntity">Type of db entity being fetched</typeparam>
    /// <typeparam name="TRequest">Type of mediatr request being page</typeparam>
    /// <typeparam name="THydra">Hydra type for each member</typeparam>
    /// <returns>
    /// ActionResult generated from FetchEntityResult. This will be the HydraCollection + 200 on success. Or a Hydra
    /// error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandleListFetch<TEntity, TRequest, THydra>(
        TRequest request,
        Func<TEntity, THydra> hydraBuilder,
        string? instance = null,
        string? errorTitle = "Fetch failed",
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<FetchEntityResult<IReadOnlyCollection<TEntity>>>
        where THydra : DlcsResource
    {
        return await HandleHydraRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return this.FetchResultToHttpResult(
                result,
                instance,
                errorTitle, results =>
                {
                    return new HydraCollection<THydra>
                    {
                        WithContext = true,
                        Members = results.Select(b => hydraBuilder(b)).ToArray(),
                        TotalItems = results.Count,
                        PageSize = results.Count,
                        Id = Request.GetJsonLdId()
                    };
                });
        }, errorTitle);
    }

    /// <summary>
    /// Make a request and handle exceptions, converting to a HydraProblem 
    /// </summary>
    protected async Task<IActionResult> HandleHydraRequest(Func<Task<IActionResult>> handler,
        string? errorTitle = "Request failed")
    {
        try
        {
            return await handler();
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

    /// <summary>
    /// Set Page and PageSize properties on specified request, reading values from query params.
    /// Page is set from ?page - if provided values is &lt;= 0 it's defaulted to 0
    /// PageSize is set from ?pageSize - if provided values is &lt;= 0 or &gt; 500 it's defaulted to PageSize setting
    /// </summary>
    /// <param name="pagedrequest">Request object to update</param>
    protected void SetPaging(IPagedRequest pagedrequest)
    {
        if (Request.Query.TryGetValue("page", out var page))
        {
            if (int.TryParse(page, out var parsedPage))
            {
                pagedrequest.Page = parsedPage;
            }
        }
        
        if (Request.Query.TryGetValue("pageSize", out var pageSize))
        {
            if (int.TryParse(pageSize, out var parsedPageSize))
            {
                pagedrequest.PageSize = parsedPageSize;
            }
        }

        if (pagedrequest.PageSize is <= 0 or > 500) pagedrequest.PageSize = Settings.PageSize;
        if (pagedrequest.Page <= 0) pagedrequest.Page = 1;
    }

    /// <summary>
    /// Set Field and Descending properties on specified request, reading properties from query params.
    /// Field is from ?orderBy or ?orderByDescending. Descending true if latter, false if former.
    /// </summary>
    /// <param name="orderableRequest">Request object to update</param>
    protected void SetOrderBy(IOrderableRequest orderableRequest)
    {
        if (Request.Query.TryGetValue("orderBy", out var orderBy))
        {
            orderableRequest.Field = orderBy;
            orderableRequest.Descending = false;
        }
        else if (Request.Query.TryGetValue("orderByDescending", out var orderByDescending))
        {
            orderableRequest.Field = orderByDescending;
            orderableRequest.Descending = true;
        }
    }
}