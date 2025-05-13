namespace DLCS.Model.Assets.NamedQueries;

/// <summary>
/// NamedQuery that is projected to IIIF Manifest
/// </summary>
public class IIIFParsedNamedQuery : ParsedNamedQuery
{
    public IIIFParsedNamedQuery(int customerId) : base(customerId)
    {
    }
}
