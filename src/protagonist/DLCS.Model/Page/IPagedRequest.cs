namespace DLCS.Model.Page;

/// <summary>
/// Represents a request for a paged resource
/// </summary>
public interface IPagedRequest
{
    int Page { get; set; }
    int PageSize { get; set; }
}