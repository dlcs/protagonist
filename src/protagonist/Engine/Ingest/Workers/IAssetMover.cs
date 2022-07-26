using DLCS.Model.Assets;
using DLCS.Model.Customers;

namespace Engine.Ingest.Workers;

/// <summary>
/// Base interface for copying Asset from Origin to new destination.
/// </summary>
public interface IAssetMover
{
    /// <summary>
    /// Copy specified asset from Origin to destination. Destination determined by implementation.
    /// </summary>
    /// <param name="asset">Asset to be copied</param>
    /// <param name="destinationTemplate">Destination location.</param>
    /// <param name="verifySize">Whether to verify that new asset-size is allowed.</param>
    /// <param name="customerOriginStrategy">Customer origin strategy to use for fetching asset.</param>
    /// <param name="cancellationToken"></param>
    /// <returns><see cref="AssetFromOrigin"/> object representing copied file.</returns>
    public Task<AssetFromOrigin> CopyAsset(Asset asset, 
        string destinationTemplate, 
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Delegate for getting <see cref="IAssetMover"/> implementation for specific type.
/// </summary>
/// <param name="type"></param>
public delegate IAssetMover AssetMoverResolver(AssetMoveType type);
        
/// <summary>
/// Represents the different types of Asset Movers.
/// </summary>
public enum AssetMoveType
{
    /// <summary>
    /// Asset to be moved to ObjectStorage (e.g. S3).
    /// </summary>
    ObjectStore,
        
    /// <summary>
    /// Asset to be copied to local disk.
    /// </summary>
    Disk
}