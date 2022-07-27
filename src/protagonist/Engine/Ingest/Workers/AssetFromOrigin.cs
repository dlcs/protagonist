using DLCS.Core.Types;
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
    public AssetId AssetId { get; }

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

    /// <summary>
    /// The location where the asset has been copied to. This may be a local disk path or an S3 location
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Mark asset as being too large and exceeding storage allowance.
    /// </summary>
    public void FileTooLarge() => FileExceedsAllowance = true;

    public AssetFromOrigin()
    {
    }

    public AssetFromOrigin(AssetId assetId, long assetSize, string location, string contentType)
    {
        AssetId = assetId;
        AssetSize = assetSize;
        Location = location;
        ContentType = contentType;
    }
}