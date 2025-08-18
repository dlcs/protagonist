using DLCS.Web.Requests.AssetDelivery;

namespace Orchestrator.Infrastructure.Mediatr;

/// <summary>
/// Marker interface for any Asset requests.
/// <see cref="AssetRequestParsingBehavior{TRequest,TResponse}"/> will parse FullPath to populate AssetRequest
/// object on subclass 
/// </summary>
public interface IAssetRequest
{
    string FullPath { get; }
}

/// <summary>
/// Marker interface for any Image asset requests
/// </summary>
public interface IImageRequest : IAssetRequest
{
    ImageAssetDeliveryRequest AssetRequest { set; }
}

/// <summary>
/// Marker interface for any asset requests
/// </summary>
public interface IGenericAssetRequest : IAssetRequest
{
    BaseAssetRequest AssetRequest { set; }
}