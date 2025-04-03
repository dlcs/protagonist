using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Model.IIIF;
using IIIF;
using Newtonsoft.Json;

namespace DLCS.Model.Assets;

/// <summary>
/// Model representing auth/open thumbnail sizes
/// </summary>
/// <remarks>This is saved as s.json in s3 and ThumbsSizes metadata in DB.</remarks>
public class ThumbnailSizes
{
    /// <summary>
    /// Default empty thumbnail sizes element
    /// </summary>
    public static readonly ThumbnailSizes Empty = new(0);
    
    [JsonProperty("o")]
    public List<int[]> Open { get; }
        
    [JsonProperty("a")]
    public List<int[]> Auth { get; }

    [JsonIgnore]
    public int Count { get; private set; }

    [JsonConstructor]
    public ThumbnailSizes(List<int[]>? open, List<int[]>? auth)
    {
        Open = open ?? new List<int[]>();
        Auth = auth ?? new List<int[]>();
        Count = Open.Count + Auth.Count;
    }
    
    public ThumbnailSizes(int sizesCount)
    {
        Open = new List<int[]>(sizesCount);
        Auth = new List<int[]>(sizesCount);
    }

    public void AddAuth(Size size)
    {
        Count++;
        Auth.Add(size.ToArray());
    }

    public void AddOpen(Size size)
    {
        Count++;
        Open.Add(size.ToArray());
    }
}

public static class ThumbnailSizesX
{
    /// <summary>
    /// Get a list of all available sizes (Auth and Open)
    /// </summary>
    public static IEnumerable<int[]> GetAllSizes(this ThumbnailSizes sizes)
        => sizes.Auth.Union(sizes.Open);

    /// <summary>
    /// Get boolean representing whether this instance is empty (ie contains no thumbs)
    /// </summary>
    public static bool IsEmpty(this ThumbnailSizes sizes) => sizes.Count == 0;

    /// <summary>
    /// Get the thumbnail size that is closest to specified targetSize.
    /// </summary>
    /// <param name="sizes">Current <see cref="ThumbnailSizes"/> object</param>
    /// <param name="targetSize"></param>
    /// <param name="isOpen">true if thumbnail is open, false if requires auth</param>
    /// <returns>Closest size</returns>
    /// <exception cref="NotImplementedException">Thrown if there are no open or auth thumbs</exception>
    public static Size SizeClosestTo(this ThumbnailSizes sizes, int targetSize, out bool isOpen)
    {
        if (sizes.IsEmpty())
        {
            throw new InvalidOperationException($"Cannot find closest size as {nameof(ThumbnailSizes)} empty");
        }

        var closestSize = sizes.GetAllSizes()
            .Select(Size.FromArray)
            .SizeClosestTo(targetSize);
        isOpen = sizes.Open.Any(wh => wh[0] == closestSize.Width && wh[1] == closestSize.Height);
        return closestSize;
    }
}
