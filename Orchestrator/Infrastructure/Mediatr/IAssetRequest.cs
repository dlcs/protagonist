using DLCS.Web.Requests.AssetDelivery;

namespace Orchestrator.Infrastructure.Mediatr
{
    /// <summary>
    /// Marker interface for any Asset requests.
    /// <see cref="AssetRequestParsingBehavior{TRequest,TResponse}"/> will parse FullPath to populate AssetRequest
    /// object on subclass 
    /// </summary>
    public interface IAssetRequest
    {
        string FullPath { get; }
    }
    
    public interface IFileRequest : IAssetRequest
    {
        FileAssetDeliveryRequest AssetRequest { set; }
    }
}