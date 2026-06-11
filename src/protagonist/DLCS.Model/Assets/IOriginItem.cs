namespace DLCS.Model.Assets;

public interface IOriginItem
{
    /// <summary>
    /// Contains source to ingest from
    /// </summary>
    string? Origin { get; }
    
    /// <summary>
    /// When implemented in a class, returns the specific item's identifier (e.g. some_file.xml or picture.tif)
    /// </summary>
    string ItemId { get; }
    
    /// <summary>
    /// Returns a human-readable identifier used in logging
    /// </summary>
    string Identifier();
}
