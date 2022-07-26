namespace DLCS.Model.Assets.Thumbs;

public enum ReorganiseResult
{
    /// <summary>
    /// Default
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Asset with specified key was not found
    /// </summary>
    AssetNotFound,
    
    /// <summary>
    /// Key already has expected layout
    /// </summary>
    HasExpectedLayout,
    
    /// <summary>
    /// Layout was successfully reorganised.
    /// </summary>
    Reorganised
}