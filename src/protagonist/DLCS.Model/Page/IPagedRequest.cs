using DLCS.Model.Assets;

namespace DLCS.Model.Page;

/// <summary>
/// Represents a request for a paged resource
/// </summary>
public interface IPagedRequest
{
    int Page { get; set; }
    int PageSize { get; set; }
}
/// <summary>
/// Marker interface for requests that can be filtered by Asset Query Syntax
/// </summary>
public interface IAssetFilterableRequest
{
    AssetQueryModel AssetQueryModel { get; }
}

/// <summary>
/// Represents a request that can be ordered
/// </summary>
public interface IOrderableRequest
{
    string? Field { get; set; }
    bool Descending { get; set; }
}
