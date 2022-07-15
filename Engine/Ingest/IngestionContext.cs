using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Entities;
using Engine.Ingest.Workers;

namespace Engine.Ingest;

/// <summary>
/// Context for an in-flight ingestion request.
/// </summary>
public class IngestionContext
{
    public Asset Asset { get; }
    
    public AssetId AssetId { get; }
            
    // TODO - should this be typed?
    public AssetFromOrigin AssetFromOrigin { get; private set; }
        
    public ImageLocation? ImageLocation { get; private set; }
        
    public ImageStorage? ImageStorage { get; private set; }
    
    public IngestionContext(Asset asset)
    {
        Asset = asset;
        AssetId = asset.GetAssetId();
    }
    
    public IngestionContext(Asset asset, AssetFromOrigin assetFromOrigin)
    {
        Asset = asset;
        AssetId = asset.GetAssetId();
        AssetFromOrigin = assetFromOrigin;
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
        
    public IngestionContext WithStorage(ImageStorage imageStorage)
    {
        ImageStorage = imageStorage.ThrowIfNull(nameof(imageStorage));
        return this;
    }
}