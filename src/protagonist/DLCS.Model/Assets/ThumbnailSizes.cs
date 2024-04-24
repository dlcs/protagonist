using System.Collections.Generic;
using System.Linq;
using IIIF;
using Newtonsoft.Json;

namespace DLCS.Model.Assets;

/// <summary>
/// Model representing auth/open thumbnail sizes
/// </summary>
/// <remarks>This is saved as s.json in s3 and ThumbsSizes metadata in DB.</remarks>
public class ThumbnailSizes
{
    [JsonProperty("o")]
    public List<int[]> Open { get; }
        
    [JsonProperty("a")]
    public List<int[]> Auth { get; }

    [JsonIgnore]
    public int Count { get; private set; }

    [JsonConstructor]
    public ThumbnailSizes(List<int[]> open, List<int[]> auth)
    {
        Open = open;
        Auth = auth;
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
}