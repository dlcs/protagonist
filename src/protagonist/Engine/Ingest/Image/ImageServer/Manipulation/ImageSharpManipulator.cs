namespace Engine.Ingest.Image.ImageServer.Manipulation;

public class ImageSharpManipulator : IImageManipulator
{
    public async Task<SixLabors.ImageSharp.Image> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        return await SixLabors.ImageSharp.Image.LoadAsync(path, cancellationToken);
    }
}