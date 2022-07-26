using System.Collections.Generic;
using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries;

/// <summary>
/// Basic parsed named query required for objects that are persisted to storage after generation and monitored
/// via a control-file
/// </summary>
public class StoredParsedNamedQuery : ParsedNamedQuery
{
    public StoredParsedNamedQuery(CustomerPathElement customerPathElement) : base(customerPathElement)
    {
    }

    /// <summary>
    /// The format for the output object saved to storage
    /// </summary>
    public string? ObjectNameFormat { get; set; }

    /// <summary>
    /// The parsed and formatted output object to save to storage
    /// </summary>
    public string ObjectName { get; set; } = "Untitled";

    /// <summary>
    /// A list of all args provided to NQ
    /// </summary>
    public List<string> Args { get; set; } = new();

    /// <summary>
    /// The storage key for generated binary 
    /// </summary>
    public string StorageKey { get; set; }

    /// <summary>
    /// The storage key for object control-file 
    /// </summary>
    public string ControlFileStorageKey { get; set; }
}