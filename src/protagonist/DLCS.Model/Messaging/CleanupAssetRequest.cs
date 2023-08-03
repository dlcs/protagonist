using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;

namespace DLCS.Model.Messaging;

public class CleanupAssetRequest
{
    /// <summary>
    /// An Asset Id
    /// </summary>
    public AssetId? Id { get; init; }

    /// <summary>
    /// List of delivery channels
    /// </summary>
    public List<string>? DeliveryChannels { get; init; }

    /// <summary>
    /// The asset family
    /// </summary>
    public char? AssetFamily { get; init; }

    /// <summary>
    /// The customer path element
    /// </summary>
    public CustomerPathElement? CustomerPathElement { get; init; }
}