using System;
using IIIF.ImageApi;

namespace DLCS.Web.IIIF;

public static class ImageRequestX
{
    private const string DefaultQuality = "default";
    private const string ColorQuality = "color";
    private const string JpgFormat = "jpg";

    /// <summary>
    /// Check if the IIIF ImageRequest has request parameter that are able to be handled by Thumbnail service.
    /// Note: This checks Format, Quality, Rotation etc - this check may pass but thumbs still cannot handle due to size
    /// constraints
    /// </summary>
    /// <param name="request">Candidate <see cref="ImageRequest"/></param>
    /// <param name="invalidMessage">String detailing why object cannot be handled</param>
    /// <returns>True if object can be handled, else false</returns>
    public static bool IsCandidateForThumbHandling(this ImageRequest request, out string? invalidMessage)
    {
        invalidMessage = null;
        if (!request.Format.Equals(JpgFormat, StringComparison.OrdinalIgnoreCase))
        {
            invalidMessage = $"Requested format '{request.Format}' not supported, use 'jpg'";
            return false;
        }
        
        if (request.Quality is not (DefaultQuality or ColorQuality))
        {
            invalidMessage = $"Requested quality '{request.Quality}' not supported, use 'default' or 'color'";
            return false;
        }

        if (request.Rotation is not { Angle: 0, Mirror: not true })
        {
            invalidMessage = "Requested rotation value not supported, use '0'";
            return false;
        }

        if (request.Size.PercentScale.HasValue)
        {
            invalidMessage = "Requested pct: size value not supported";
            return false;
        }

        return true;
    }
}