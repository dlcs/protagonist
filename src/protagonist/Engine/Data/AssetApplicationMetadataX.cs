using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Engine.Data;

public static class AssetApplicationMetadataX
{
    /// <summary>
    /// Upsert application metadata for given type. Note that this requires the asset to be loaded with
    /// AssetApplicationMetadata.
    /// Updates provided <see cref="Asset"/> only, no DB writes take place.
    /// Provided metadata value is serialized using newtonsoft
    /// </summary>
    public static AssetApplicationMetadata UpsertApplicationMetadata<T>(this Asset asset, string metadataType,
        T metadataValue) where T : class
        => asset.UpsertApplicationMetadata(metadataType, JsonConvert.SerializeObject(metadataValue));
    
    /// <summary>
    /// Upsert application metadata for given type. Note that this requires the asset to be loaded with
    /// AssetApplicationMetadata.
    /// Updates provided <see cref="Asset"/> only, no DB writes take place.
    /// </summary>
    public static AssetApplicationMetadata UpsertApplicationMetadata(this Asset asset, string metadataType,
        string metadataValue)
    {
        ValidateJson(metadataValue);
        var existingMetadata = asset.AssetApplicationMetadata?.FirstOrDefault(e => e.MetadataType == metadataType);

        var now = DateTime.UtcNow;
        if (existingMetadata is not null)
        {
            existingMetadata.MetadataValue = metadataValue;
            existingMetadata.Modified = now;
            return existingMetadata;
        }

        asset.AssetApplicationMetadata ??= new List<AssetApplicationMetadata>();
        var assetApplicationMetadata = new AssetApplicationMetadata
        {
            MetadataType = metadataType,
            MetadataValue = metadataValue,
            Created = now,
            Modified = now
        };
        asset.AssetApplicationMetadata.Add(assetApplicationMetadata);

        return assetApplicationMetadata;
    }

    private static void ValidateJson(string metadataValue)
    {
        // Safety check - validate what's passed is valid JSON
        JToken.Parse(metadataValue);
    }
}
