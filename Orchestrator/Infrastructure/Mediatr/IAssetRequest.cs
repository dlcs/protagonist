using DLCS.Web.Requests.AssetDelivery;

namespace Orchestrator.Infrastructure.Mediatr
{
    /// <summary>
    /// Marker interface for any Asset requests.
    /// Request Pipeline will parse FullPath to populate AssetRequest object 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAssetRequest<T> : IAssetRequest
        where T : BaseAssetRequest, new()
    {
        /// <summary>
        /// Basic request Path
        /// </summary>
        //string FullPath { get; }
        
        T? AssetRequest { set; }
    }
    
    public interface IFileRequest : IAssetRequest
    {
        FileAssetDeliveryRequest AssetRequest { set; }
    }

    public interface IAssetRequest
    {
        string FullPath { get; }
    }
}