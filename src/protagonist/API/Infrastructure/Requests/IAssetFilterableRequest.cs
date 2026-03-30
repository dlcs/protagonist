using API.Features.Assets.Query;

namespace API.Infrastructure.Requests;

/// <summary>
/// Marker interface for requests that can be filtered by Asset Query Syntax
/// </summary>
public interface IAssetFilterableRequest
{
    AssetQueryModel AssetQueryModel { get; }
}
