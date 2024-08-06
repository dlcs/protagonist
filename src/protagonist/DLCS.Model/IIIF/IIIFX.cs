using System;
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
    /// Use <see cref="SizeParameter"/> values to resize <see cref="Size"/> object.
    ///
    /// Note that this isn't an exhaustive method - it'll only support the allowed sizeParam values, as reflected in
    /// <see cref="IsValidThumbnailParameter"/>
    /// </summary>
    public static Size ResizeIfSupported(this SizeParameter sizeParameter, Size assetSize)
    {
        if (!sizeParameter.IsValidThumbnailParameter())
        {
            throw new InvalidOperationException($"Attempt to resize using unsupported SizeParameter: {sizeParameter}");
        }

        return sizeParameter.Resize(assetSize, InvalidUpscaleBehaviour.ReturnOriginal);
    }
    
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