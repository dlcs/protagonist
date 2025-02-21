using System;
using System.Globalization;
using DLCS.Core.Types;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Response;

public static class HttpResponseX
{
    /// <summary>
    /// Return a seeOther response (HTTP 303) to the client
    /// </summary>
    /// <param name="httpResponse">Current <see cref="HttpResponse"/> object</param>
    /// <param name="location">The URL to redirect the client to. This must be properly encoded for use in http headers
    /// where only ASCII characters are allowed.</param>
    public static void SeeOther(this HttpResponse httpResponse, string location)
    {
        httpResponse.StatusCode = 303;
        httpResponse.Headers["Location"] = location;
    }
    
    public static void AppendStandardNoCacheHeaders(this HttpResponse response)
    {
        response.Headers.Append("Cache-Control", "no-cache, s-maxage=0, max-age=0");
        response.Headers.Append("Pragma", "no-cache");
        response.Headers.Append("Expires", DateTime.Now.Date.ToString("r", DateTimeFormatInfo.InvariantInfo));
    }

    public static void CacheForDays(this HttpResponse response, int days)
    {
        response.CacheForSeconds(days * 86400);
    }

    public static void CacheForHours(this HttpResponse response, int hours)
    {
        response.CacheForSeconds(hours * 3600);
    }

    public static void CacheForMinutes(this HttpResponse response, int minutes)
    {
        response.CacheForSeconds(minutes * 60);
    }

    public static void CacheForSeconds(this HttpResponse response, int seconds)
    {
        const string template = "public, s-maxage={0}, max-age={0}";
        response.Headers.Append("Cache-Control", String.Format(template, seconds));
    }

    /// <summary>
    /// Set the x-asset-id header to AssetId value
    /// </summary>
    public static void SetAssetIdResponseHeader(this HttpResponse response, AssetId assetId)
    {
        const string assetIdHeader = "x-asset-id";
        response.Headers[assetIdHeader] = assetId.ToString();
    } 
}