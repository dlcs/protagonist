namespace DLCS.Model.Assets;

public interface IOriginItem
{
    /// <summary>
    /// Contains source to ingest from
    /// </summary>
    string? Origin { get; set; }
    
    /// <summary>
    /// Returns a human-readable identifier used in logging
    /// </summary>
    string Identifier();
}
