namespace API.Converters;

/// <summary>
/// To construct a Hydra response object, you need the protocol and domain of the API itself,
/// and the protocol and domain of the public facing resources (e.g., Image Services).
/// </summary>
public class UrlRoots
{
    /// <summary>
    /// The base URI for current request - this is the full URI excluding path and query string
    /// </summary>
    public string BaseUrl { get; set; }
    
    /// <summary>
    /// The base URI for image services and other public-facing resources
    /// </summary>
    public string ResourceRoot { get; set; }
}