namespace API.Infrastructure.Requests;

/// <summary>
/// Represents a request for a paged resource
/// </summary>
public interface IPagedRequest
{
    /// <summary>
    /// The page number
    /// </summary>
    int Page { get; set; }
    
    /// <summary>
    /// The page size
    /// </summary>
    int PageSize { get; set; }
}
