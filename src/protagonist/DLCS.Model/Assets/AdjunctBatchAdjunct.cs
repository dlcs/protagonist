using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public class AdjunctBatchAdjunct
{
    public int BatchId { get; set; }
    public string AdjunctId { get; set; } = null!;
    public AssetId AdjunctAssetId { get; set; }
    public AdjunctBatch Batch { get; set; } = null!;
}
