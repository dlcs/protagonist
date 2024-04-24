namespace Engine.Ingest.Image.ImageServer.Measuring;

public class ImageSharpMeasurer : IImageMeasurer
{
    public async Task<ImageOnDisk> MeasureImage(string path, CancellationToken cancellationToken = default)
    {
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(path, cancellationToken);
        var imageOnDisk = new ImageOnDisk
        {
            Path = path,
            Width = image.Width,
            Height = image.Height
        };
        return imageOnDisk;
    }
}