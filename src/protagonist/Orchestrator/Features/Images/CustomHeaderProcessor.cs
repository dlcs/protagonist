using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Model.Assets.CustomHeaders;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Features.Images;

/// <summary>
/// Processes <see cref="CustomHeader"/> and sets any headers in <see cref="IProxyActionResult"/>
/// </summary>
public static class CustomHeaderProcessor
{
    public static void SetProxyImageHeaders(
        List<CustomHeader> customerCustomHeaders,
        OrchestrationImage orchestrationImage,
        IProxyActionResult proxyImageServerResult)
    {
        // order of precedence (low -> high), same header will be overwritten if present
        // no space or role
        // matching space and no role
        // no space and a matching role
        // matching space and a matching role
        if (customerCustomHeaders.IsNullOrEmpty()) return;

        var imageSpace = orchestrationImage.AssetId.Space;
        
        SetHeader(customerCustomHeaders.Where(ch => (ch.Space ?? -1) == -1 && string.IsNullOrEmpty(ch.Role)));
        SetHeader(customerCustomHeaders.Where(ch => ch.Space == imageSpace && string.IsNullOrEmpty(ch.Role)));

        if (!orchestrationImage.RequiresAuth) return;

        SetHeader(customerCustomHeaders.Where(ch =>
            (ch.Space ?? -1) == -1 && orchestrationImage.Roles.Contains(ch.Role)));
        
        SetHeader(customerCustomHeaders.Where(ch =>
            ch.Space == imageSpace && orchestrationImage.Roles.Contains(ch.Role)));
        
        void SetHeader(IEnumerable<CustomHeader> filteredCustomHeaders)
        {
            foreach (var header in filteredCustomHeaders)
            {
                proxyImageServerResult.WithHeader(header.Key, header.Value);
            }
        }
    }
}