namespace DLCS.Model.Assets;

public static class DeliverableX
{
    /// <summary>
    /// Reset fields for ingestion, marking as "Ingesting" and clearing errors
    /// </summary>
    public static void SetFieldsForIngestion(this IDeliverable asset)
    {
        asset.Error = string.Empty;
        asset.Ingesting = true;
    }
}
