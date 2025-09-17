using DLCS.Core.Types;
using Engine.Ingest.Image.ImageServer.Models;

namespace Engine.Ingest.Image.ImageServer.Clients;

public interface IImageProcessorClient
{
    /// <summary>
    /// Calls appetiser to generate an image
    /// </summary>
    /// <param name="modifiedAssetId">The modified asset id</param>
    /// <param name="context">ingestion context for the request</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A response containing details of the generated image</returns>
    public Task<IAppetiserResponse> GenerateJP2(
        IngestionContext context, 
        AssetId modifiedAssetId,   
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a JP2 filepath for an image
    /// </summary>
    /// <param name="assetId">The asset id used to retrieve the JP2 filepath</param>
    /// <param name="ingestId">The id for the ingest operation associated with this image</param>
    /// <param name="forImageProcessor">Whether this is for the image processor or not</param>
    /// <returns></returns>
    public string GetJP2FilePath(AssetId assetId, string ingestId, bool forImageProcessor);
}
