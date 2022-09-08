using DLCS.Model.Assets;

namespace API.Infrastructure.Models;

/// <summary>
/// API-specific extension methods for entities
/// </summary>
public static class EntityX
{
    /// <summary>
    /// Reset fields for ingestion, marking as "Ingesting" and clearing errors
    /// </summary>
    public static void SetFieldsForIngestion(this Asset asset)
    {
        asset.Error = string.Empty;
        asset.Ingesting = true;
    }
}