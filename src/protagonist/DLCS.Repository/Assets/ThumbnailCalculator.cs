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
        var requestWidth = imageRequest.Size.Width ?? 0;
        var requestHeight = imageRequest.Size.Height ?? 0;
        
        if (requestWidth > 0 && requestHeight > 0)
        {
            // get the longest dimension of the requested size
            var max = Math.Max(requestWidth, requestHeight);
            var foundExactSize = sizes.Exists(s => 
                s.Width == requestWidth &&
                s.Height == requestHeight);

            // We found a size that matches the request exactly, so we'll go with that
            if (foundExactSize)
            {
                return new SizeCandidate(max);
            }
            
            // If the image is confined, are there any sizes that fit?
            if (imageRequest.Size.Confined)
            {
                // Pick the first thumbnail size as a reference for shape
                var shape = sizes.First().GetShape();

                // If this is a landscape image, max should match its width
                if (shape == ImageShape.Landscape 
                    && sizes.Exists(s => s.Width == max && requestHeight >= s.Height))
                {
                    return new SizeCandidate(max);
                }
                // For portrait images, max should match its height
                else if (shape == ImageShape.Portrait
                    && sizes.Exists(s => s.Height == max && requestWidth >= s.Width))
                {
                    return new SizeCandidate(max);
                }
                // Lastly, for square images, min should match both dimensions instead
                else if (shape == ImageShape.Square)
                {
                    var min = Math.Min(requestWidth, requestHeight);
                    if (sizes.Exists(s => s.Width == min))
                    {
                        return new SizeCandidate(min);
                    }
                }
            }
            
            // Otherwise, resize
            return new SizeCandidate();
        }
        
        if (imageRequest.Size.Max)
        {
            return new SizeCandidate(sizes[0].MaxDimension);
        }

        // we need to know the sizes of things...
        int? longestEdge = null;
        if (requestWidth > 0)
        {
            foreach (var size in sizes)
            {
                if (size.Width == requestWidth)
                {
                    longestEdge = size.MaxDimension;
                    break;
                }
            }
        }

        if (requestHeight > 0)
        {
            foreach (var size in sizes)
            {
                if (size.Height == requestHeight)
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