using System;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

/// <summary>
/// An individual item within an <see cref="IDeliverableBatch"/>
/// </summary>
public interface IDeliverableBatchItem
{
    int BatchId { get; set; }
    AssetId AssetId { get; set; }
    BatchStatus Status { get; set; }
    string? Error { get; set; }
    DateTime? Finished { get; set; }
}
