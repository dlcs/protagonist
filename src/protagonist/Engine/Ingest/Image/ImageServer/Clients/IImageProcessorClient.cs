using DLCS.Core.Types;
using Engine.Ingest.Image.ImageServer.Models;
using IIIF.ImageApi;

namespace Engine.Ingest.Image.ImageServer.Clients;

public interface IImageProcessorClient
{
    /// <summary>
    /// Calls image-processor to generate a image derivatives
    /// </summary>
    /// <param name="modifiedAssetId">The modified asset id (ie safe to use in file path)</param>
    /// <param name="context">Ingestion context for the request</param>
    /// <param name="thumbnailSizes">A list of IIIF SizeParameters for thumbnail sizes</param>
    /// <param name="options">Image processing instructions</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A response containing details of the generated image(s)</returns>
    public Task<IImageProcessorResponse> GenerateDerivatives(
        IngestionContext context, 
        AssetId modifiedAssetId,
        IReadOnlyList<SizeParameter> thumbnailSizes,
        ImageProcessorOperations options,
        CancellationToken cancellationToken = default);
}


/// <summary>
/// Flags enum for specifying which operations should be run by image processor
/// </summary>
[Flags]
public enum ImageProcessorOperations
{
    /// <summary>
    /// Fallback
    /// </summary>
    None = 1,
    
    /// <summary>
    /// Image processor should generate thumbnails
    /// </summary>
    Thumbnails = 2,
    
    /// <summary>
    /// Convert incoming file to JP2 derivative
    /// </summary>
    Derivative = 4,
}
