using DLCS.Model.Customers;

namespace Engine.Ingest.Workers;

/// <summary>
/// An asset that has been copied from Origin.
/// </summary>
public class AssetFromOrigin
{
    /// <summary>
    /// The DLCS asset id.
    /// </summary>
    public string AssetId { get; }

    /// <summary>
    /// The size of the asset in bytes.
    /// </summary>
    public long AssetSize { get; }

    /// <summary>
    /// The type of the asset.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// The customer origin strategy used to process this asset.
    /// </summary>
    public CustomerOriginStrategy CustomerOriginStrategy { get; set; }

    /// <summary>
    /// Whether the asset will exceed the storage policy allowance.
    /// </summary>
    public bool FileExceedsAllowance { get; private set; }

    public string Location { get; set; }

    /// <summary>
    /// Mark asset as being too large and exceeding storage allowance.
    /// </summary>
    public void FileTooLarge() => FileExceedsAllowance = true;

    public AssetFromOrigin()
    {
    }

    public AssetFromOrigin(string assetId, long assetSize, string location, string contentType)
    {
        AssetId = assetId;
        AssetSize = assetSize;
        Location = location;
        ContentType = contentType;
    }
}