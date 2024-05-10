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
    /// Thrown if thumbs policy not found and throwIfNotFound = true
    /// </exception>
    public static ThumbnailSizes? GetThumbsMetadata(this ICollection<AssetApplicationMetadata>? metadata,
        bool throwIfNotFound = false)
    {
        var thumbsMetadata =
            metadata?.SingleOrDefault(md => md.MetadataType == AssetApplicationMetadataTypes.ThumbSizes);

        if (thumbsMetadata == null)
        {
            return throwIfNotFound
                ? throw new InvalidOperationException("Thumbs metadata not found")
                : null;
        }
        
        return JsonConvert.DeserializeObject<ThumbnailSizes>(thumbsMetadata.MetadataValue);
    }
}