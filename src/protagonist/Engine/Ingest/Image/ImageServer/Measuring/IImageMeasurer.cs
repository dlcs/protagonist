namespace Engine.Ingest.Image.ImageServer.Measuring;

public interface IImageMeasurer
{
    /// <summary>
    /// Return <see cref="ImageOnDisk"/> object image at specified path 
    /// </summary>
    public Task<ImageOnDisk> MeasureImage(string path, CancellationToken cancellationToken = default);
}