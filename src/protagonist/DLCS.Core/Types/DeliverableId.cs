namespace DLCS.Core.Types;

/// <summary>
/// Identifier for a deliverable DLCS resource - either an Asset or an Adjunct of an Asset
/// </summary>
public class DeliverableId
{
    /// <summary>Id of Asset, or parent Asset if this is an adjunct</summary>
    public AssetId AssetId { get; }

    /// <summary>Id of Adjunct, null if this identifies an Asset</summary>
    public string? AdjunctId { get; }

    /// <summary>Whether this identifies an Adjunct, rather than an Asset</summary>
    public bool IsAdjunct => AdjunctId != null;

    /// <summary>
    /// Identifier for a deliverable DLCS resource - either an Asset or an Adjunct of an Asset
    /// </summary>
    /// <param name="assetId">Id of Asset, or parent Asset if this is an Adjunct</param>
    /// <param name="adjunctId">Id of Adjunct, null if this identifies an Asset</param>
    public DeliverableId(AssetId assetId, string? adjunctId = null)
    {
        AssetId = assetId;
        AdjunctId = adjunctId;
    }

    /// <summary>
    /// Get string representation, in format customer/space/asset or customer/space/asset/adjunct
    /// </summary>
    public override string ToString() => IsAdjunct ? $"{AssetId}/{AdjunctId}" : AssetId.ToString();

    public static implicit operator DeliverableId(AssetId assetId) => new(assetId);
}
