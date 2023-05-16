using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi;

namespace DLCS.Repository.Assets;

public static class ThumbnailCalculator
{
    /// <summary>
    /// Get a list of all 
    /// </summary>
    /// <param name="sizes"></param>
    /// <param name="imageRequest"></param>
    /// <param name="allowResize"></param>
    /// <returns></returns>
    public static SizeCandidate GetCandidate(List<Size> sizes, ImageRequest imageRequest, bool allowResize)
    {
        return allowResize
            ? GetLongestEdgeAndSize(sizes, imageRequest)
            : GetLongestEdge(sizes, imageRequest);
    }

    private static SizeCandidate GetLongestEdge(List<Size> sizes, ImageRequest imageRequest)
    {
        if (imageRequest.Size.Width > 0 && imageRequest.Size.Height > 0)
        {
            // We don't actually need to check imageRequest.Size.Confined (!w,h) because same logic applies...
            var max = Math.Max(imageRequest.Size.Width ?? 0, imageRequest.Size.Height ?? 0);
            return sizes.Select(s => s.MaxDimension).Contains(max)
                ? new SizeCandidate(max)
                : new SizeCandidate();
        }
        
        if (imageRequest.Size.Max)
        {
            return new SizeCandidate(sizes[0].MaxDimension);
        }

        // we need to know the sizes of things...
        int? longestEdge = null;
        if (imageRequest.Size.Width > 0)
        {
            foreach (var size in sizes)
            {
                if (size.Width == imageRequest.Size.Width)
                {
                    longestEdge = size.MaxDimension;
                    break;
                }
            }
        }

        if (imageRequest.Size.Height > 0)
        {
            foreach (var size in sizes)
            {
                if (size.Height == imageRequest.Size.Height)
                {
                    longestEdge = size.MaxDimension;
                    break;
                }
            }
        }

        return new SizeCandidate(longestEdge);
    }

    private static ResizableSize GetLongestEdgeAndSize(List<Size> sizes, ImageRequest imageRequest)
    {
        // TODO - handle there being none "open"?
        
        var sizeCandidate = GetLongestEdge(sizes, imageRequest);
        if (sizeCandidate.KnownSize)
        {
            // We have found a matching size, use that.
            return new ResizableSize(sizeCandidate.LongestEdge.Value);
        }
        
        // calculate the size using requested dimensions
        Size idealSize;
        var sizeParameter = imageRequest.Size;
        if (sizeParameter.Confined)
        {
            var requestSize = new Size(imageRequest.Size.Width!.Value, imageRequest.Size.Height!.Value);
            idealSize = Size.FitWithin(requestSize, sizes[0]);
        }
        else
        {
            idealSize = Size.Resize(sizes[0], sizeParameter.Width, sizeParameter.Height);
        }

        // iterate through all of the known sizes until we find a smaller one
        int count = 0;
        Size? larger = null;
        foreach (var s in sizes)
        {
            // Iterate until we find one that is smaller
            if (!idealSize.IsConfinedWithin(s))
            {
                break;
            }

            larger = s;
            count++;
        }

        return new ResizableSize
        {
            Ideal = idealSize,
            LargerSize = larger,
            SmallerSize = count == sizes.Count ? null : sizes[count]
        };
    }
}