namespace API.Infrastructure.Requests;

/// <summary>
/// Marker interface for operations that alter an underlying entity
/// </summary>
public interface IModifyRequest
{
    /// <summary>
    /// Whether record is deemed as success or not
    /// </summary>
    bool IsSuccess { get; }
}