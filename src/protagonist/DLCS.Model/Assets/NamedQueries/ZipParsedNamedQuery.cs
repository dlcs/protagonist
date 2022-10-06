using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries;

/// <summary>
/// NamedQuery that is projected to Zip archive
/// </summary>
public class ZipParsedNamedQuery : StoredParsedNamedQuery
{
    public ZipParsedNamedQuery(CustomerPathElement customerPathElement) : base(customerPathElement)
    {
    }
}