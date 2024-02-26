using System.Collections.Generic;
using DLCS.HydraModel;
using NuGet.ContentModel;

namespace API.Features.Image;

public class AssetBeforeProcessing
{
    public DLCS.Model.Assets.Asset Asset { get; init; } = null!;

    public DeliveryChannel[]? DeliveryChannels { get; init; }
}