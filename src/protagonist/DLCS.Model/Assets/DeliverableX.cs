using System;

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
    
    /// <summary>
    /// Mark asset as finished, setting "Finished" and "Ingesting" = false 
    /// </summary>
    public static void MarkAsFinished(this IDeliverable asset)
    {
        asset.Ingesting = false;
        asset.Finished = DateTime.UtcNow;
    }
}
