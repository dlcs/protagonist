using DLCS.Core.Types;
using Engine.Settings;

namespace Engine.Ingest.Persistence;

public static class StorageX
{
    /// <summary>
    /// Format the "Asset" part of assetId to escape characters that can cause issues when saving to disk. E.g. ( and )
    /// </summary>
    public static string GetDiskSafeAssetId(this AssetId assetId, ImageIngestSettings imageIngestSettings)
        => assetId.Asset
            .Replace("(", imageIngestSettings.OpenBracketReplacement)
            .Replace(")", imageIngestSettings.CloseBracketReplacement);
}
