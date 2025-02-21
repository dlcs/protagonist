namespace Engine.Ingest.Image;

/// <summary>
/// Generates image derivatives and converts source image to alternative format. 
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Generate thumbnails and/or tile-optimised image file.
    /// Copy generated files to destination 'slow' storage, if appropriate. 
    /// </summary>
    /// <param name="context">Object representing current ingestion operation.</param>
    /// <returns>true if succeeded, else false.</returns>
    Task<bool> ProcessImage(IngestionContext context);
}