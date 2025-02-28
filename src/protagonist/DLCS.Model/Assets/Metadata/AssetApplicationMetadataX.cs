using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DLCS.Model.Assets.Metadata;

public static class AssetApplicationMetadataX
{
    /// <summary>
    /// Get deserialised <see cref="ThumbnailSizes"/> for thumbs metadata
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if thumbs data not found and throwIfNotFound = true
    /// </exception>
    public static ThumbnailSizes? GetThumbsMetadata(this ICollection<AssetApplicationMetadata>? metadata,
        bool throwIfNotFound = false) =>
        metadata.GetMetadata<ThumbnailSizes>(AssetApplicationMetadataTypes.ThumbSizes, throwIfNotFound);

    /// <summary>
    /// Get deserialised <see cref="AVTranscode"/> list for AV transcode metadata
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if thumbs data not found and throwIfNotFound = true
    /// </exception>
    public static AVTranscode[]? GetTranscodeMetadata(this ICollection<AssetApplicationMetadata>? metadata,
        bool throwIfNotFound = false) =>
        metadata.GetMetadata<AVTranscode[]>(AssetApplicationMetadataTypes.AVTranscodes, throwIfNotFound);

    private static T? GetMetadata<T>(this ICollection<AssetApplicationMetadata>? metadata, string metadataType,
        bool throwIfNotFound = false)
        where T : class
    {
        var typedMetadata = metadata?.SingleOrDefault(md => md.MetadataType == metadataType);

        if (typedMetadata == null)
        {
            return throwIfNotFound
                ? throw new InvalidOperationException($"'{metadataType}' metadata not found")
                : null;
        }

        return JsonConvert.DeserializeObject<T>(typedMetadata.MetadataValue);
    }
}
