namespace API.Infrastructure.Requests;

/// <summary>
/// Represents a request that can be ordered
/// </summary>
public interface IOrderableRequest
{
    string? Field { get; set; }
    bool Descending { get; set; }
}
