namespace DLCS.Repository.NamedQueries;

public class NamedQueryTemplateSettings
{
    /// <summary>
    /// String format for generating keys for PDF object storage.
    /// Supported replacements are {customer}/{queryname}/{args}
    /// </summary>
    public string PdfStorageTemplate { get; set; } = "{customer}/pdf/{queryname}/{args}";

    /// <summary>
    /// String format for generating keys for Zip object storage.
    /// Supported replacements are {customer}/{queryname}/{args}
    /// </summary>
    public string ZipStorageTemplate { get; set; } = "{customer}/zip/{queryname}/{args}";
}