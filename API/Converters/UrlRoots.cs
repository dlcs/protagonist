namespace API.Converters;

/// <summary>
/// To construct a Hydra response object, you need the protocol and domain of the API itself,
/// and the protocol and domain of the public facing resources (e.g., Image Services).
/// </summary>
public class UrlRoots
{
    public string BaseUrl { get; set; }
    public string ResourceRoot { get; set; }
}