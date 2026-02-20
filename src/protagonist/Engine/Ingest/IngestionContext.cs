using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Strategy;
using Engine.Ingest.Persistence;

namespace Engine.Ingest;

public class AdjunctIngestionContext(Adjunct adjunct) : IngestionContext(adjunct.Asset)
{
    public Adjunct Adjunct { get; } = adjunct;

    public override IOriginItem GetOriginItem() => Adjunct;

    public override AssetFromOrigin CreateAssetFromOrigin(long received, string location, string contentType)
        => new AdjunctFromOrigin(Adjunct.Id, AssetId, received, location, contentType);

    public override string GetMediaType() => Adjunct.MediaType;
}

/// <summary>
/// Context for an in-flight ingestion request.
/// </summary>
public class IngestionContext(Asset asset)
{
    public Asset Asset { get; } = asset;

    public AssetId AssetId { get; } = asset.Id;

    public string IngestId { get; } = DateTime.Now.Ticks.ToString();

    public AssetFromOrigin? AssetFromOrigin { get; private set; }

    public ImageLocation? ImageLocation { get; private set; }

    public ImageStorage? ImageStorage { get; private set; }

    public long PreIngestionAssetSize { get; private set; }

    /// <summary>
    /// Any objects, and their size, uploaded to DLCS storage
    /// </summary>
    public Dictionary<ObjectInBucket, long> StoredObjects { get; } = new();

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
    /// Retrieves the <see cref="IOriginItem"/> that is subject to ingestion
    /// </summary>
    public virtual IOriginItem GetOriginItem() => Asset;

    /// <summary>
    /// Overridable factory for creating appropriate result for this ingestion
    /// </summary>
    /// <param name="received">bytes received when ingesting item</param>
    /// <param name="location">location where the item has been stored</param>
    /// <param name="contentType">content type of the item</param>
    public virtual AssetFromOrigin CreateAssetFromOrigin(long received, string location, string contentType)
        => new(AssetId, received, location, contentType);

    /// <summary>
    /// Retrieves media type of the <see cref="IOriginItem"/> that's subject to ingestion, if available
    /// </summary>
    public virtual string? GetMediaType() => Asset.MediaType;

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

    public IngestionContext WithStorage(long? assetSize = null, long? thumbnailSize = null, long? adjunctSize = null)
    {
        ImageStorage ??= new ImageStorage
        {
            Id = AssetId,
            Customer = AssetId.Customer,
            Space = AssetId.Space,
        };

        ImageStorage.Size += assetSize ?? 0;
        ImageStorage.AdjunctSize += adjunctSize ?? 0;
        ImageStorage.ThumbnailSize += thumbnailSize ?? 0;
        ImageStorage.LastChecked = DateTime.UtcNow;

        return this;
    }

    /// <summary>
    /// Updates the media type to value from origin if it is the Protagonist fallback value
    /// </summary>
    public IngestionContext UpdateMediaTypeIfRequired()
    {
        if (AssetFromOrigin == null) return this;

        if (Asset.MediaType == MIMEHelper.UnknownImage && !AssetFromOrigin.ContentType.IsNullOrEmpty())
        {
            Asset.MediaType = AssetFromOrigin.ContentType;
        }

        return this;
    }
}
