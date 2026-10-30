using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Infrastructure.Auth.V2;

/// <summary>
/// Client for interactions with IIIF Authorization Flow 2 services
/// </summary>
/// <remarks>
/// If roles contain an IP role, the client IP is forwarded to the auth service via x-forwarded-for as it is used to
/// grant IP roles
/// </remarks>
public class IIIFAuth2Client(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<IIIFAuth2Client> logger) : IIIIFAuthBuilder
{
    private const string ForwardedForHeader = "X-Forwarded-For";

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

    public async Task<bool> VerifyAccess(IAccessControlledOrchestrationItem orchestrationItem, CancellationToken cancellationToken)
    {
        var deliverableId = orchestrationItem.DeliverableId;
        var path = $"verifyaccess/{deliverableId}?roles={GetRolesString(orchestrationItem.Roles)}";
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
            AddForwardedForIfRequired(httpRequest, orchestrationItem.Roles);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying access for {DeliverableId}", deliverableId);
            return false;
        }   
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

    private static string GetRolesString(IReadOnlyList<string> roles)
        => roles.Count == 1
            ? roles[0]
            : string.Join(",", roles.Where(r => !r.Equals(Asset.UnobtainableRole, StringComparison.OrdinalIgnoreCase)));
}
