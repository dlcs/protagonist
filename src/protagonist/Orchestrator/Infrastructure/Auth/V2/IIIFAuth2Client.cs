using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Auth;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.Auth.V2;

/// <summary>
/// Client for interactions with IIIF Authorization Flow 2 services
/// </summary>
/// <remarks>
/// If roles contain an IP role, the client IP is forwarded to the auth service via x-forwarded-for as it is used to
/// grant IP roles. VerifyAccess results are cached for a short period, see <see cref="VerifyAccess"/>
/// </remarks>
public class IIIFAuth2Client(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IAppCache appCache,
    AuthCookieManager authCookieManager,
    IOptions<OrchestratorSettings> orchestratorOptions,
    ILogger<IIIFAuth2Client> logger) : IIIIFAuthBuilder
{
    private const string ForwardedForHeader = "X-Forwarded-For";

    /// <summary>
    /// Default ttl for granted verifyaccess results, if no <see cref="CacheOverrideKeys.VerifyAccess"/> override
    /// configured. Preconfigured cache durations are too long for auth results
    /// </summary>
    private const int DefaultVerifyAccessTtlSecs = 5;

    /// <summary>
    /// Default ttl for denied verifyaccess results, if no <see cref="CacheOverrideKeys.VerifyAccessDenied"/> override
    /// configured. Shorter than granted so a user is not denied for long after signing in
    /// </summary>
    private const int DefaultVerifyAccessDeniedTtlSecs = 2;

    private readonly CacheSettings cacheSettings = orchestratorOptions.Value.Caching;

    public async Task<IService?> GetAuthServices(DeliverableId deliverableId, IReadOnlyList<string> roles,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Getting auth 2 services description for {DeliverableId}, {@Roles}", deliverableId, roles);

        if (roles.ContainsOnly(Asset.UnobtainableRole)) return null;

        var rolesString = GetRolesString(roles);
        var path = $"services/{deliverableId}?roles={rolesString}";
        try
        {
            await using var authServices = await httpClient.GetStreamAsync(path, cancellationToken);
            var authProbeService = authServices.FromJsonStream<AuthProbeService2>();
            return authProbeService;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting IIIF Auth2 Services for {DeliverableId}, {Roles}", deliverableId,
                rolesString);
            return null;
        }
    }

    /// <summary>
    /// Get <see cref="AuthProbeResult2"/> from auth service.
    /// </summary>
    /// <param name="accessToken">
    /// Optional access token, if null no Authorization header is sent. Only used without access token if roles
    /// contain an IP role, otherwise auth service is not called and missing credentials result returned
    /// </param>
    public async Task<AuthProbeResult2> GetProbeServiceResult(DeliverableId deliverableId, IReadOnlyList<string> roles,
        string? accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(accessToken) && !roles.ContainsIpRole())
        {
            logger.LogDebug("No access token and no IP roles for {DeliverableId}, not calling auth service",
                deliverableId);
            return AuthProbeResult2Builder.MissingCredentials;
        }

        var rolesString = GetRolesString(roles);
        var path = $"probe_internal/{deliverableId}?roles={rolesString}";
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            AddForwardedForIfRequired(httpRequest, roles);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var probeServiceResult = contentStream.FromJsonStream<AuthProbeResult2>();
            return probeServiceResult.ThrowIfNull(nameof(probeServiceResult));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting IIIF Probe Service2 for {DeliverableId}, {Roles}", deliverableId,
                rolesString);
            return AuthProbeResult2Builder.UnexpectedError;
        }
    }

    /// <summary>
    /// Verify whether current request can access specified item.
    /// </summary>
    /// <remarks>
    /// Results are cached for a short period, configurable via <see cref="CacheOverrideKeys.VerifyAccess"/> (granted)
    /// and <see cref="CacheOverrideKeys.VerifyAccessDenied"/> (denied) overrides, 0 to disable. Keyed on deliverable,
    /// roles, client IP and Auth2 cookie value, so results are never shared between users. Concurrent requests with the
    /// same key result in a single call to auth service. Errors are never cached.
    /// </remarks>
    public async Task<bool> VerifyAccess(IAccessControlledOrchestrationItem orchestrationItem,
        CancellationToken cancellationToken)
    {
        var grantedTtl = cacheSettings.GetTtl(named: CacheOverrideKeys.VerifyAccess,
            defaultTtl: DefaultVerifyAccessTtlSecs);
        var deniedTtl = cacheSettings.GetTtl(named: CacheOverrideKeys.VerifyAccessDenied,
            defaultTtl: DefaultVerifyAccessDeniedTtlSecs);
        if (grantedTtl <= 0 && deniedTtl <= 0)
        {
            return await CallVerifyAccess(orchestrationItem, cancellationToken) ?? false;
        }

        var cacheKey = GetVerifyAccessCacheKey(orchestrationItem);

        // Low priority as cheap to recreate. Expiration is overwritten in factory once result is known, ttl here only
        // needs to be valid (> 0)
        var cacheOptions = cacheSettings.GetMemoryCacheOptions(priority: CacheItemPriority.Low,
            defaultTtl: Math.Max(grantedTtl, 1));
        try
        {
            return await appCache.GetOrAddAsync(cacheKey, async entry =>
            {
                // Result is shared by concurrent callers so not cancelled by any one of them
                var canAccess = await CallVerifyAccess(orchestrationItem, CancellationToken.None) ??
                                throw new VerifyAccessFailedException();

                var ttl = canAccess ? grantedTtl : deniedTtl;
                entry.AbsoluteExpirationRelativeToNow = ttl > 0 ? TimeSpan.FromSeconds(ttl) : TimeSpan.FromTicks(1);
                return canAccess;
            }, cacheOptions);
        }
        catch (VerifyAccessFailedException)
        {
            // Error already logged. Throwing from the factory means the failed result isn't cached
            return false;
        }
    }

    /// <summary>
    /// Call verifyaccess. Returns true if access granted, false if denied (401/403) or null if an error occurred
    /// </summary>
    private async Task<bool?> CallVerifyAccess(IAccessControlledOrchestrationItem orchestrationItem,
        CancellationToken cancellationToken)
    {
        var deliverableId = orchestrationItem.DeliverableId;
        var path = $"verifyaccess/{deliverableId}?roles={GetRolesString(orchestrationItem.Roles)}";
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
            AddForwardedForIfRequired(httpRequest, orchestrationItem.Roles);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return false;

            logger.LogError("Unexpected status {StatusCode} verifying access for {DeliverableId}",
                response.StatusCode, deliverableId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying access for {DeliverableId}", deliverableId);
            return null;
        }
    }

    /// <summary>
    /// Get cache key for verifyaccess result. This includes everything that the result depends on: deliverable,
    /// roles, client IP (only if roles contain an IP role, as it's only forwarded then) and Auth2 cookie value (hashed)
    /// </summary>
    private string GetVerifyAccessCacheKey(IAccessControlledOrchestrationItem orchestrationItem)
    {
        var deliverableId = orchestrationItem.DeliverableId;
        var roles = GetRolesString(orchestrationItem.Roles);

        var httpContext = httpContextAccessor.HttpContext;
        var clientIp = orchestrationItem.Roles.ContainsIpRole()
            ? httpContext?.Connection.RemoteIpAddress?.ToString() ?? "none"
            : "-";

        var cookieValue = authCookieManager.GetAuth2CookieValueForCustomer(deliverableId.AssetId.Customer);
        var cookieHash = string.IsNullOrEmpty(cookieValue)
            ? "none"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookieValue)));

        return $"auth2-va:{deliverableId}:{roles}:{clientIp}:{cookieHash}";
    }

    /// <summary>
    /// Add x-forwarded-for header containing client IP, if roles contain an IP role. The client IP is
    /// RemoteIpAddress, which will have been set from incoming x-forwarded-for by forwarded headers middleware
    /// </summary>
    private void AddForwardedForIfRequired(HttpRequestMessage httpRequest, IReadOnlyList<string> roles)
    {
        if (!roles.ContainsIpRole()) return;

        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (clientIp == null)
        {
            logger.LogDebug("Roles contain IP role but unable to determine client IP");
            return;
        }

        httpRequest.Headers.Add(ForwardedForHeader, clientIp.ToString());
    }

    /// <summary>
    /// Get canonical, comma-separated roles string. Roles are distinct and ordered so the same set of roles always
    /// results in the same string, the unobtainable role is excluded unless it's the only role
    /// </summary>
    private static string GetRolesString(IReadOnlyList<string> roles)
        => string.Join(",", roles
            .Where(r => roles.Count == 1 || !r.Equals(Asset.UnobtainableRole, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Order(StringComparer.Ordinal));

    /// <summary>
    /// Thrown from cache factory when verifyaccess call fails, to prevent result being cached
    /// </summary>
    private sealed class VerifyAccessFailedException : Exception;
}
