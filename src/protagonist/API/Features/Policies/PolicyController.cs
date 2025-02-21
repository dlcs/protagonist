using API.Features.Policies.Converters;
using API.Features.Policies.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using Hydra.Collections;
using Hydra.Model;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Policies;

/// <summary>
/// Controller for handling requests for imageOptimisationPolicies, storagePolicies and thumbnailPolicies
/// </summary>
public class PolicyController : HydraController
{
    /// <inheritdoc />
    public PolicyController(IOptions<ApiSettings> settings, IMediator mediator) : base(settings.Value, mediator)
    {
    }
    
    /// <summary>
    /// Get paged list of all global image optimisation policies.
    ///
    /// Supports ?page= and ?pageSize= query parameters
    /// </summary>
    /// <returns>Collection of Hydra JSON-LD image optimisation policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/imageOptimisationPolicies")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<ImageOptimisationPolicy>))]
    public async Task<IActionResult> GetImageOptimisationPolicies(CancellationToken cancellationToken)
    {
        var getImageOptimisationPolicies = new GetImageOptimisationPolicies();

        return await HandlePagedFetch<DLCS.Model.Policies.ImageOptimisationPolicy, GetImageOptimisationPolicies,
            ImageOptimisationPolicy>(
            getImageOptimisationPolicies,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get image optimisation policies failed",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Get details of specified image optimisation policy
    /// </summary>
    /// <returns>Hydra JSON-LD image optimisation policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/imageOptimisationPolicies/{imageOptimisationPolicyId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImageOptimisationPolicy))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetImageOptimisationPolicy([FromRoute] string imageOptimisationPolicyId, CancellationToken cancellationToken)
    {
        var getImageOptimisationPolicy = new GetImageOptimisationPolicy(imageOptimisationPolicyId);

        return await HandleFetch(
            getImageOptimisationPolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get image optimisation policy failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get paged list of all storage policies
    ///
    /// Supports ?page= and ?pageSize= query parameters
    /// </summary>
    /// <returns>Collection of Hydra JSON-LD storage policy objects</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET: /storagePolicies
    ///     GET: /storagePolicies?page=2&pagesize=10
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [Route("/storagePolicies")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<StoragePolicy>))]
    public async Task<IActionResult> GetStoragePolicies(CancellationToken cancellationToken)
    {
        var getStoragePolicies = new GetStoragePolicies();

        return await HandlePagedFetch<DLCS.Model.Storage.StoragePolicy, GetStoragePolicies,
            StoragePolicy>(
            getStoragePolicies,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get storage policies failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get details of specified storage policy
    /// </summary>
    /// <returns>Hydra JSON-LD storage policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/storagePolicies/{storagePolicyId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StoragePolicy))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetStoragePolicy([FromRoute] string storagePolicyId, CancellationToken cancellationToken)
    {
        var getStoragePolicy = new GetStoragePolicy(storagePolicyId);

        return await HandleFetch(
            getStoragePolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get storage policy failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get paged list of all thumbnail policies
    ///
    /// Supports ?page= and ?pageSize= query parameters
    /// </summary>
    /// <returns>Collection of Hydra JSON-LD image optimisation policy objects</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET: /thumbnailPolicies
    ///     GET: /thumbnailPolicies?page=2&pagesize=10
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [Route("/thumbnailPolicies")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<ThumbnailPolicy>))]
    public async Task<IActionResult> GetThumbnailPolicies(CancellationToken cancellationToken)
    {
        var getThumbnailPolicies = new GetThumbnailPolicies();

        return await HandlePagedFetch<DLCS.Model.Policies.ThumbnailPolicy, GetThumbnailPolicies, ThumbnailPolicy>(
            getThumbnailPolicies,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get thumbnail policies failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get details of specified thumbnail policy
    /// </summary>
    /// <returns>Hydra JSON-LD storage policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/thumbnailPolicies/{thumbnailPolicyId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ThumbnailPolicy))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    public async Task<IActionResult> GetThumbnailPolicy([FromRoute] string thumbnailPolicyId, 
        CancellationToken cancellationToken)
    {
        var getThumbnailPolicy = new GetThumbnailPolicy(thumbnailPolicyId);

        return await HandleFetch(
            getThumbnailPolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get thumbnail policy failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get paged list of all origin strategies
    ///
    /// Supports ?page= and ?pageSize= query parameters
    /// </summary>
    /// <returns>Collection of Hydra JSON-LD origin strategy objects</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET: /originStrategies
    ///     GET: /originStrategies?page=2&pagesize=10
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [Route("/originStrategies")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HydraCollection<OriginStrategy>))]
    public async Task<IActionResult> GetOriginStrategies(CancellationToken cancellationToken)
    {
        var getOriginStrategies = new GetOriginStrategies();

        return await HandlePagedFetch<DLCS.Model.Policies.OriginStrategy, GetOriginStrategies, OriginStrategy>(
            getOriginStrategies,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get origin strategies failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// Get details of specified origin strategy
    /// </summary>
    /// <param name="originStrategyId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD origin strategy object</returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OriginStrategy))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Error))]
    [HttpGet]
    [AllowAnonymous]
    [Route("/originStrategies/{originStrategyId}")]
    public async Task<IActionResult> GetOriginStrategy([FromRoute] string originStrategyId, 
        CancellationToken cancellationToken)
    {
        var getStrategy = new GetOriginStrategy(originStrategyId);

        return await HandleFetch(
            getStrategy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get origin strategy failed",
            cancellationToken: cancellationToken
        );
    }
}