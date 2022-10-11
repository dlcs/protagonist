namespace DLCS.Model.Assets.NamedQueries;

/// <summary>
/// NamedQuery that is projected to PDF
/// </summary>
public class PdfParsedNamedQuery : StoredParsedNamedQuery
{
    public PdfParsedNamedQuery(int customerId) : base(customerId)
    {
    }

    /// <summary>
    /// Format of URL of existing pdf to be used as coverpage for generated pdf
    /// </summary>
    public string? CoverPageFormat { get; set; }
    
    /// <summary>
    /// URL of existing pdf to be used as coverpage for generated pdf
    /// </summary>
    public string? CoverPageUrl { get; set; }

    /// <summary>
    /// Message to show on any pages that have items that require auth.
    /// </summary>
    public string RedactedMessage { get; set; } = "Unable to display page";
}