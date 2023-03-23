using DLCS.AWS.S3.Models;
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
    
    public AssetFromOrigin? AssetFromOrigin { get; private set; }
        
    public ImageLocation? ImageLocation { get; private set; }
        
    public ImageStorage? ImageStorage { get; private set; }
    
    [Obsolete("Use StoredObjects")]
    public List<ObjectInBucket> UploadedKeys { get; } = new();
    
    /// <summary>
    /// Any objects, and their size, uploaded to DLCS storage
    /// </summary>
    public Dictionary<ObjectInBucket, long> StoredObjects { get; } = new();
    
    public IngestionContext(Asset asset)
    {
        Asset = asset;
        AssetId = asset.Id;
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
        
    [Obsolete("Use overload that takes values")]
    public IngestionContext WithStorage(ImageStorage imageStorage)
    {
        ImageStorage = imageStorage.ThrowIfNull(nameof(imageStorage));
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
}