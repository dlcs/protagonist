namespace DLCS.Model.Assets;

public interface IDeliverable
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
    /// Contains source to ingest from
    /// </summary>
    string? Origin { get; set; }
}
