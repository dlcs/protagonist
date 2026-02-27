using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using IIIF.ImageApi;

namespace DLCS.Web.IIIF;

public static class ImageRequestX
{
    /// <summary>
    /// A list of accepted IIIF image qualities
    /// </summary>
    /// <remarks>https://iiif.io/api/image/3.0/#quality</remarks>
    private static class Qualities
    {
        public const string Color = "color";
        public const string Gray = "gray";
        public const string Bitonal = "bitonal";
        public const string Default = "default";

        public static readonly string[] All = [Color, Gray, Bitonal, Default];
        
        public static readonly string AllCsv = string.Join(',', All);
    }
    
    /// <summary>
    /// A list of accepted IIIF image formats
    /// </summary>
    /// <remarks>https://iiif.io/api/image/3.0/#45-format</remarks>
    private static class Formats
    {
        public const string Jpg = "jpg";
        public const string Tif = "tif";
        public const string Gif = "gif";
        public const string Png = "png";

        public static readonly string[] All = [Jpg, Tif, Gif, Png];

        public static readonly string AllCsv = string.Join(',', All);
    }

    /// <summary>
    /// Check if the IIIF ImageRequest is a supported format, quality and non-zero size.  
    /// </summary>
    /// <param name="request">Candidate <see cref="ImageRequest"/></param>
    /// <param name="invalidMessage">String detailing why object cannot be handled</param>
    /// <returns>True if object can be handled, else false</returns>
    /// <remarks>This is a quick check - request may fail later in processing chain</remarks>
    public static bool IsCandidateForImageHandling(this ImageRequest request,
        [NotNullWhen(false)] out string? invalidMessage)
    {
        invalidMessage = null;
        if (!Formats.All.Contains(request.Format, StringComparer.OrdinalIgnoreCase))
        {
            invalidMessage = $"Requested format '{request.Format}' not supported, must be one of '{Formats.AllCsv}'";
            return false;
        }

        if (!Qualities.All.Contains(request.Quality, StringComparer.OrdinalIgnoreCase))
        {
            invalidMessage = $"Requested quality '{request.Quality}' not supported, must be one of '{Qualities.AllCsv}'";
            return false;
        }

        var size = request.Size;
        if (!((size.Width ?? 1) > 0 && (size.Height ?? 1) > 0))
        {
            invalidMessage = "Requested size must be greater than 0";
            return false;
        }

        return true;
    }

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
        if (!request.Format.Equals(Formats.Jpg, StringComparison.OrdinalIgnoreCase))
        {
            invalidMessage = $"Requested format '{request.Format}' not supported, use '{Formats.Jpg}'";
            return false;
        }
        
        if (request.Quality is not (Qualities.Default or Qualities.Color))
        {
            invalidMessage =
                $"Requested quality '{request.Quality}' not supported, use '{Qualities.Default}' or '{Qualities.Color}'";
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

    /// <summary>
    /// For the given <see cref="ImageRequest"/>, get the {region}/{size}/{rotation}/{quality}.{format} only (ie no
    /// identifier or prefix etc).
    /// </summary>
    /// <remarks>
    /// This doesn't make any checks on whether the request has all required properties, or is for info.json etc
    /// </remarks>
    public static string GetImageRequestOnly(this ImageRequest request)
        => $"{request.Region}/{request.Size}/{request.Rotation}/{request.Quality}.{request.Format}";
}
