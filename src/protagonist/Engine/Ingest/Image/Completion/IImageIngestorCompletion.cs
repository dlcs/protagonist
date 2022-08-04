namespace Engine.Ingest.Image.Completion;

/// <summary>
/// Interface for operations to be carried out when Ingestion has been completed.
/// </summary>
public interface IImageIngestorCompletion
{
    /// <summary>
    /// Final operations to when ingestion has been completed.
    /// </summary>
    /// <returns>true if operations completed successfully, else false.</returns>
    Task<bool> CompleteIngestion(IngestionContext context, bool ingestSuccessful, string? sourceTemplate);
}