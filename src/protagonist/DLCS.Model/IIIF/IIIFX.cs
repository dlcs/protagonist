using System;
using IIIF.ImageApi;

namespace DLCS.Model.IIIF;

/// <summary>
/// Extension methods for iiif-net 
/// </summary>
public static class IIIFX
{
    /// <summary>
    /// Get the maximum dimension (width or height) for size parameter
    /// </summary>
    public static int GetMaxDimension(this SizeParameter sizeParameter)
        => Math.Max(sizeParameter.Width ?? 0, sizeParameter.Height ?? 0);
}