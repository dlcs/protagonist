namespace Engine.Ingest.Image.ImageServer.Manipulation;

public interface IImageManipulator
{
    public Task<SixLabors.ImageSharp.Image> LoadAsync(string path, CancellationToken cancellationToken = default);
}