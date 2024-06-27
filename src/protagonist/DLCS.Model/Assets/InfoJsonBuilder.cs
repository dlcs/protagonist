using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;

namespace DLCS.Model.Assets;

/// <summary>
/// Contains methods for building info.json responses.
/// </summary>
public static class InfoJsonBuilder
{
    /// <summary>
    /// Get level 0 info.json object for IIIF Image 2.1
    /// </summary>
    /// <param name="serviceEndpoint">URI for image</param>
    /// <param name="sizes">List of sizes image is available in.</param>
    /// <returns>info.json object</returns>
    public static ImageService2 GetImageApi2_1Level0(string serviceEndpoint, List<int[]> sizes)
    {
        var imageService = new ImageService2
        {
            Context = ImageService2.Image2Context,
            Id = serviceEndpoint,
            Type = IIIF.Constants.ImageService2Type,
            Protocol = ImageService2.Image2Protocol,
            Profile = ImageService2.Level0Profile,
            ProfileDescription = new ProfileDescription
            {
                Formats = new[] { "jpg" },
                Qualities = new[] { "color" },
                Supports = new[] { "sizeByWhListed" }
            },
            Sizes = GetSizesOrderedAscending(sizes)
        };
        imageService.Width = imageService.Sizes[^1].Width;
        imageService.Height = imageService.Sizes[^1].Height;
        return imageService;
    }

    /// <summary>
    /// Get full info.json for use by image-services for IIIF Image 2.1
    /// </summary>
    /// <param name="serviceEndpoint">URI for image</param>
    /// <param name="sizes">List of sizes image is available in</param>
    /// <param name="width">Width of image</param>
    /// <param name="height">Height of image</param>
    /// <returns>info.json string</returns>
    public static ImageService2 GetImageApi2_1Level1(string serviceEndpoint, int width, int height,
        List<int[]> sizes)
    {
        var imageService = new ImageService2
        {
            Context = ImageService2.Image2Context,
            Id = serviceEndpoint,
            Type = IIIF.Constants.ImageService2Type,
            Protocol = ImageService2.Image2Protocol,
            Profile = ImageService2.Level1Profile,
            Width = width,
            Height = height,
            ProfileDescription = new ProfileDescription
            {
                Formats = new[] { "jpg" },
                Qualities = new[] { "native", "color", "gray" },
                Supports = new[]
                {
                    "regionByPct", "sizeByForcedWh", "sizeByWh", "sizeAboveFull", "rotationBy90s", "mirroring",
                    "gray"
                }
            },
            Sizes = GetSizesOrderedAscending(sizes)
        };
        imageService.Tiles = GetTiles(imageService.Width, imageService.Height);
        return imageService;
    }

    /// <summary>
    /// Get level 0 info.json object for IIIF Image 3
    /// </summary>
    /// <param name="serviceEndpoint">URI for image</param>
    /// <param name="sizes">List of sizes image is available in.</param>
    /// <returns>info.json object</returns>
    public static ImageService3 GetImageApi3_Level0(string serviceEndpoint, List<int[]> sizes, int? width = null,
        int? height = null)
    {
        // TODO ExtraFeatures may need altering if resizing is supported 
        var imageService = new ImageService3
        {
            Context = ImageService3.Image3Context,
            Id = serviceEndpoint,
            Protocol = ImageService3.ImageProtocol,
            Profile = ImageService3.Level0Profile,
            ExtraFeatures = new List<string> { Features.ProfileLinkHeader, Features.JsonldMediaType },
            PreferredFormats = new List<string> { "jpg" },
            Sizes = GetSizesOrderedAscending(sizes)
        };
        imageService.Width = width ?? imageService.Sizes[^1].Width;
        imageService.Height = height ?? imageService.Sizes[^1].Height;
        return imageService;
    }

    private static List<Size> GetSizesOrderedAscending(List<int[]> sizes)
        => sizes.OrderBy(wh => wh[0]).Select(wh => Size.FromArray(wh)).ToList();

    public static List<Tile> GetTiles(int width, int height, int tileSize = 256)
    {
        var scaleFactors = GetScaleFactorSizes(width, height, tileSize);
        return new List<Tile>
        {
            new()
            {
                Height = tileSize, Width = tileSize, ScaleFactors = scaleFactors.ToArray()
            }
        };
    }

    private static IEnumerable<int> GetScaleFactorSizes(int width, int height, int tileSize)
    {
        var max = Math.Max(width, height);
        var factors = new List<int> { 1 };
        while (max > tileSize)
        {
            max /= 2;
            factors.Add(factors[^1] * 2);
        }
        return factors;
    }
}
