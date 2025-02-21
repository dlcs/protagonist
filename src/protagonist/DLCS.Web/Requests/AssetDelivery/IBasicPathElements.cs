namespace DLCS.Web.Requests.AssetDelivery;

/// <summary>
/// Interface containing basic required elements for generating URLs
/// </summary>
public interface IBasicPathElements
{
    /// <summary>
    /// The request root, e.g. "thumbs", "iiif-img" etc.
    /// </summary>
    public string RoutePrefix { get; }
    
    /// <summary>
    /// Optional version value, e.g. "v2", "v3" 
    /// </summary>
    public string? VersionPathValue { get; }
    
    /// <summary>
    /// The "customer" value from request (int or string value). 
    /// </summary>
    public string CustomerPathValue { get; }
    
    /// <summary>
    /// The Space for this request.
    /// </summary>
    public int Space { get; }
    
    /// <summary>
    /// The AssetPath for this request, this is everything after space. e.g.
    /// my-image/full/61,100/0/default.jpg
    /// my-audio/full/full/max/max/0/default.mp4
    /// file-identifier
    /// </summary>
    public string AssetPath { get; }
}

public static class BasicPathElementsX
{
    /// <summary>
    /// Get a new <see cref="BasicPathElements"/> object created from current <see cref="IBasicPathElements"/> object
    /// </summary>
    /// <param name="elements"><see cref="IBasicPathElements"/> to clone</param>
    /// <returns>New <see cref="BasicPathElements"/> object</returns>
    public static BasicPathElements CloneBasicPathElements(this IBasicPathElements elements)
    => new()
    {
        Space = elements.Space,
        AssetPath = elements.AssetPath,
        RoutePrefix = elements.RoutePrefix,
        CustomerPathValue = elements.CustomerPathValue,
        VersionPathValue = elements.VersionPathValue
    };
}
