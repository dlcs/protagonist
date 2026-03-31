namespace API.Infrastructure.Requests;

/// <summary>
/// Represents a request that can be ordered
/// </summary>
public interface IOrderableRequest
{
    /// <summary>
    /// The field to order by
    /// </summary>
    string? Field { get; set; }
    
    /// <summary>
    /// Whether to order descending (true) or ascending (false)
    /// </summary>
    bool Descending { get; set; }
}
