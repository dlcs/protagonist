using DLCS.Core.Guard;
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
            
    // TODO - should this be typed?
    public AssetFromOrigin AssetFromOrigin { get; }
        
    public ImageLocation? ImageLocation { get; private set; }
        
    public ImageStorage? ImageStorage { get; private set; }
    
    public IngestionContext(Asset asset, AssetFromOrigin assetFromOrigin)
    {
        Asset = asset;
        AssetFromOrigin = assetFromOrigin;
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