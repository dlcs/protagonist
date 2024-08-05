using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IIIF;
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

    /// <summary>
    /// From provided sizes, return the Size that has MaxDimension closest to specified targetSize
    ///
    /// e.g. [[100, 200], [250, 500] [500, 1000]], targetSize = 800 would return [500, 1000]
    /// </summary>
    /// <param name="sizes">List of sizes to query</param>
    /// <param name="targetSize">Ideal MaxDimension to find</param>
    /// <returns><see cref="Size"/> closes to specified value</returns>
    public static Size SizeClosestTo(this IEnumerable<Size> sizes, int targetSize)
    {
        var closestSize = sizes
            .OrderBy(s => s.MaxDimension)
            .Aggregate((x, y) =>
                Math.Abs(x.MaxDimension - targetSize) < Math.Abs(y.MaxDimension - targetSize) ? x : y);
        return closestSize;
    }
    
    /// <summary>
    /// Validate whether <see cref="SizeParameter"/> is valid as a thumbnail policy
    ///
    /// We do not support: max, pct or non-confining w,h (ie /w,h/ or /^w,h/)
    /// </summary>
    public static bool IsValidThumbnailParameter(this SizeParameter param) => param switch
    {
        { Max: true } => false,
        { PercentScale: not null } => false,
        { Confined: false, Width: not null, Height: not null } => false,
        { Confined: true } and ({ Width: null } or { Height : null }) => false,
        { Width: null, Height: null } => false,
        _ => true,
    };
}