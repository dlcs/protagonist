using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Infrastructure.Auth.V2;

/// <summary>
/// Client for interactions with IIIF Authorization Flow 2 services
/// </summary>
public class IIIFAuth2Client : IIIIFAuthBuilder
{
    private readonly HttpClient httpClient;
    private readonly ILogger<IIIFAuth2Client> logger;

    public IIIFAuth2Client(HttpClient httpClient, ILogger<IIIFAuth2Client> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }
    
    public async Task<IService?> GetAuthServicesForAsset(AssetId assetId, List<string> roles, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Getting auth 2 services description for {AssetId}, {@Roles}", assetId, roles);
        var path = $"services/{assetId}?roles={GetRolesString(roles)}";

        try
        {
            await using var authServices = await httpClient.GetStreamAsync(path, cancellationToken);
            var authProbeService = authServices.FromJsonStream<AuthProbeService2>();
            return authProbeService;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting IIIF Auth2 Services for {AssetId}", assetId);
            return null;
        }
    }

    public async Task<AuthProbeResult2> GetProbeServiceResult(AssetId assetId, List<string> roles, string accessToken,
        CancellationToken cancellationToken)
    {
        var path = $"probe_internal/{assetId}?roles={GetRolesString(roles)}";
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var probeServiceResult = contentStream.FromJsonStream<AuthProbeResult2>();
            return probeServiceResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting IIIF Probe Service2 for {AssetId}", assetId);
            return AuthProbeResult2Builder.UnexpectedError;
        }
    }

    public async Task<bool> VerifyAccess(AssetId assetId, List<string> roles, CancellationToken cancellationToken)
    {
        var path = $"verifyaccess/{assetId}?roles={GetRolesString(roles)}";
        try
        {
            var response = await httpClient.GetAsync(path, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying access for {AssetId}", assetId);
            return false;
        }   
    }

    private static string GetRolesString(IList<string> roles)
        => roles.Count == 1 ? roles[0] : string.Join(",", roles);
}