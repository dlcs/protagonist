using System;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public interface IDeliverable : IOriginItem
{
    /// <summary>
    /// Marks this object as being currently ingested
    /// </summary>
    bool? Ingesting { get; set; }
    
    /// <summary>
    /// Records any errors encountered during latest processing of the object
    /// </summary>
    string? Error { get; set; }
    
    /// <summary>
    /// When the item has last finished processing
    /// </summary>
    DateTime? Finished { get; set; }

    /// <summary>
    /// Returns <see cref="AssetId"/> that is or is parent to this deliverable
    /// </summary>
    AssetId GetAssetId();

    /// <summary>
    /// Returns the id of the deliverable
    /// </summary>
    string Identifier();
}
