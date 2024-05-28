using DLCS.AWS.S3.Models;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest.Persistence;

namespace Engine.Ingest;

/// <summary>
/// Context for an in-flight ingestion request.
/// </summary>
public class IngestionContext
{
    public Asset Asset { get; }
    
    public AssetId AssetId { get; }
    
    public string IngestId { get; }
    
    public AssetFromOrigin? AssetFromOrigin { get; private set; }
        
    public ImageLocation? ImageLocation { get; private set; }
        
    public ImageStorage? ImageStorage { get; private set; }
    
    public long PreIngestionAssetSize { get; private set; }
    
    /// <summary>
    /// Any objects, and their size, uploaded to DLCS storage
    /// </summary>
    public Dictionary<ObjectInBucket, long> StoredObjects { get; } = new();
    
    public IngestionContext(Asset asset)
    {
        Asset = asset;
        AssetId = asset.Id;
        IngestId = DateTime.Now.Ticks.ToString();
    }
    
    public IngestionContext WithAssetFromOrigin(AssetFromOrigin assetFromOrigin)
    {
        AssetFromOrigin = assetFromOrigin;
        return this;
    }
    
    public IngestionContext WithLocation(ImageLocation imageLocation)
    {
        ImageLocation = imageLocation.ThrowIfNull(nameof(imageLocation));
        return this;
    }

    /// <summary>
    /// Updates the pre-ingestion asset size.  This is used for calculating storage of reingested assets
    /// </summary>
    /// <param name="assetSize">The size of the asset</param>
    /// <returns>The ingestion context</returns>
    public IngestionContext WithPreIngestionAssetSize(long? assetSize = null)
    {
        PreIngestionAssetSize = assetSize ?? 0;
        
        return this;
    }
    
    public IngestionContext WithStorage(long? assetSize = null, long? thumbnailSize = null)
    {
        ImageStorage ??= new ImageStorage
        {
            Id = AssetId,
            Customer = AssetId.Customer,
            Space = AssetId.Space,
        };

        ImageStorage.Size += assetSize ?? 0;
        ImageStorage.ThumbnailSize += thumbnailSize ?? 0;
        ImageStorage.LastChecked = DateTime.UtcNow;
        
        return this;
    }

    /// <summary>
    /// Updates the media type if it's been set as something the caller didn't understand
    /// </summary>
    /// <returns></returns>
    public IngestionContext UpdateMediaTypeIfRequired()
    {
        if (AssetFromOrigin == null) return this;
        
        if (Asset.MediaType == "image/unknown" && !AssetFromOrigin.ContentType.IsNullOrEmpty())
        {
            Asset.MediaType = AssetFromOrigin.ContentType;
        }

        return this;
    }
}