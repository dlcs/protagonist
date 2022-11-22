using API.Features.Policies.Converters;
using API.Features.Policies.Requests;
using API.Infrastructure;
using API.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Features.Policies;

/// <summary>
/// Controller for handling requests for imageOptimisationPolicies, storagePolicies and thumbnailPolicies for customers
/// </summary>
[Route("/customers/{customerId}")]
[ApiController]
public class CustomerPolicyController : HydraController
{
    /// <inheritdoc />
    public CustomerPolicyController(IOptions<ApiSettings> settings, IMediator mediator) : base(settings.Value, mediator)
    {
    }

    /// <summary>
    /// Get paged list of all customer accessible image optimisation policies (customer specific + global).
    ///
    /// Supports ?page= and ?pageSize= query parameters
    /// </summary>
    /// <returns>Collection of Hydra JSON-LD image optimisation policy object</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET: /imageOptimisationPolicies/123
    ///     GET: /imageOptimisationPolicies/123?page=2&pagesize=10
    /// </remarks>
    [HttpGet]
    [Route("imageOptimisationPolicies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImageOptimisationPolicies([FromRoute] int customerId,
        CancellationToken cancellationToken)
    {
        var getImageOptimisationPolicies = new GetImageOptimisationPolicies(customerId);

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
    [Route("imageOptimisationPolicies/{imageOptimisationPolicyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImageOptimisationPolicy([FromRoute] int customerId,
        [FromRoute] string imageOptimisationPolicyId, CancellationToken cancellationToken)
    {
        var getImageOptimisationPolicy = new GetImageOptimisationPolicy(imageOptimisationPolicyId, customerId);

        return await HandleFetch(
            getImageOptimisationPolicy,
            policy => policy.ToHydra(GetUrlRoots().BaseUrl),
            errorTitle: "Get image optimisation policy failed",
            cancellationToken: cancellationToken
        );
    }
}