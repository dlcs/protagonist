using System;

namespace DLCS.Model.Assets;

/// <summary>
/// Represents a batch of <see cref="IDeliverable"/>s that have been submitted for processing.
/// </summary>
public interface IDeliverableBatch
{
    int Customer { get; set; }
    DateTime Submitted { get; set; }
    int Count { get; set; }
    int Completed { get; set; }
    int Errors { get; set; }
    DateTime? Finished { get; set; }
}
