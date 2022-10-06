using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries;

/// <summary>
/// NamedQuery that is projected to IIIF Manifest
/// </summary>
public class IIIFParsedNamedQuery : ParsedNamedQuery
{
    public IIIFParsedNamedQuery(CustomerPathElement customerPathElement) : base(customerPathElement)
    {
    }
    
    /// <summary>
    /// Which Asset property to use for specifying Manifest 
    /// </summary>
    /// <remarks>This is currently not used, needs to be implemented</remarks>
    public QueryMapping Manifest { get; set; } = QueryMapping.Unset;
}