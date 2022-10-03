using System.Threading;
using System.Threading.Tasks;
using API.Features.Policies.Converters;
using API.Features.Policies.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
    /// GET /imageOptimisationPolicies
    ///
    /// Get paged list of all image optimisation policies
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of Hydra JSON-LD image optimisation policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/imageOptimisationPolicies")]
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
    /// GET /imageOptimisationPolicies/{imageOptimisationPolicyId}
    /// 
    /// Get details of specified image optimisation policies
    /// </summary>
    /// <param name="imageOptimisationPolicyId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD image optimisation policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/imageOptimisationPolicies/{imageOptimisationPolicyId}")]
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
    /// GET /storagePolices
    ///
    /// Get paged list of all storage policies
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of Hydra JSON-LD storage policy objects</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/storagePolicies")]
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
    /// GET /storagePolicies/{storagePolicyId}
    /// 
    /// Get details of specified storage policy
    /// </summary>
    /// <param name="storagePolicyId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD storage policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/storagePolicies/{storagePolicyId}")]
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
    /// GET /thumbnailPolicies
    ///
    /// Get paged list of all thumbnail policies
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of Hydra JSON-LD image optimisation policy objects</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/thumbnailPolicies")]
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
    /// GET /thumbnailPolicies/{thumbnailPolicyId}
    /// 
    /// Get details of specified thumbnail policy
    /// </summary>
    /// <param name="originStrategyId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD storage policy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/thumbnailPolicies/{originStrategyId}")]
    public async Task<IActionResult> GetThumbnailPolicy([FromRoute] string originStrategyId, 
        CancellationToken cancellationToken)
    {
        var getThumbnailPolicy = new GetThumbnailPolicy(originStrategyId);

        return await HandleFetch(
            getThumbnailPolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get thumbnail policy failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// GET /originStrategies
    ///
    /// Get paged list of all origin strategies
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of Hydra JSON-LD origin strategy objects</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/originStrategies")]
    public async Task<IActionResult> GetOriginStrategies(CancellationToken cancellationToken)
    {
        var getOriginStrategies = new GetOriginStrategies();

        return await HandlePagedFetch<DLCS.Model.Policies.OriginStrategy, GetOriginStrategies, OriginStrategy>(
            getOriginStrategies,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get thumbnail policies failed",
            cancellationToken: cancellationToken
        );
    }
    
    /// <summary>
    /// GET /originStrategies/{originStrategyId}
    /// 
    /// Get details of specified origin strategy
    /// </summary>
    /// <param name="originStrategyId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Hydra JSON-LD origin strategy object</returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/originStrategies/{originStrategyId}")]
    public async Task<IActionResult> GetOriginStrategy([FromRoute] string originStrategyId, CancellationToken cancellationToken)
    {
        var getStrategy = new GetOriginStrategy(originStrategyId);

        return await HandleFetch(
            getStrategy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get thumbnail policy failed",
            cancellationToken: cancellationToken
        );
    }
}