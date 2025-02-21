using System;
using System.Collections.Generic;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;

namespace Test.Helpers.Data;

/// <summary>
/// Collection of helper methods for generating test entities
/// </summary>
public static class EntityHelpers
{
    public static Asset WithTestThumbnailMetadata(this Asset asset,
        string metadataValue = "{\"a\": [], \"o\": [[75, 100], [150, 200], [300, 400], [769, 1024]]}")
    {
        asset.AssetApplicationMetadata ??= new List<AssetApplicationMetadata>();
        asset.AssetApplicationMetadata.Add(new AssetApplicationMetadata
        {
            AssetId = asset.Id,
            MetadataType = AssetApplicationMetadataTypes.ThumbSizes,
            MetadataValue = metadataValue,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        });
        return asset;
    }
}