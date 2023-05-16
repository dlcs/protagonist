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

public interface IAssetFilterableRequest
{
    AssetFilter? AssetFilter { get; }
}

/// <summary>
/// Represents a request that can be ordered
/// </summary>
public interface IOrderableRequest
{
    string? Field { get; set; }
    bool Descending { get; set; }
}