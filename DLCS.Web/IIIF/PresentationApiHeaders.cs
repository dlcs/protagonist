using System;
using IIIF.Presentation;
using Microsoft.AspNetCore.Http;
using Version = IIIF.Presentation.Version;

namespace DLCS.Web.IIIF;

public static class PresentationApiHeaders
{
    /// <summary>
    /// Parse Accepts headers to find requested IIIF PresentationApi version, falling back to specified version if not
    /// found.  
    /// </summary>
    /// <param name="request">Current HttpRequest</param>
    /// <param name="fallbackVersion">PresentationApi version to fallback to if no type found.</param>
    /// <returns>PresentationApi version</returns>
    public static Version GetIIIFPresentationApiVersion(this HttpRequest request, Version fallbackVersion)
    {
        var requestedVersion = request.GetTypedHeaders().Accept.GetIIIFPresentationType();
        var version = requestedVersion == Version.Unknown ? fallbackVersion : requestedVersion;
        return version;
    } 
    
    /// <summary>
    /// Get Content-Type to set for PresentationApi request
    /// </summary>
    /// <param name="request">Current HttpRequest</param>
    /// <param name="presentationApiVersion"></param>
    /// <returns></returns>
    public static string GetIIIFContentType(this HttpRequest request, Version presentationApiVersion)
        => presentationApiVersion switch
        {
            Version.V3 => ContentTypes.V3,
            Version.V2 => ContentTypes.V2,
            _ => throw new ArgumentOutOfRangeException(nameof(presentationApiVersion), presentationApiVersion,
                "Unable to determine ContentType for PresentationApi version")
        };
}