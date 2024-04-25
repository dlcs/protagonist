using SixLabors.ImageSharp;

namespace Engine.Ingest.Image.ImageServer.Measuring;

public class ImageSharpMeasurer : IImageMeasurer
{
    private readonly ILogger<ImageSharpMeasurer> logger;
    
    public ImageSharpMeasurer(ILogger<ImageSharpMeasurer> logger)
    {
        this.logger = logger;
    }
    
    public async Task<ImageOnDisk?> MeasureImage(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(path, cancellationToken);
            var imageOnDisk = new ImageOnDisk
            {
                Path = path,
                Width = image.Width,
                Height = image.Height
            };

            await using var test = new FileStream(path, FileMode.Open);
            test.Seek(10, SeekOrigin.End);
            using var testTwo = await SixLabors.ImageSharp.Image.LoadAsync(test, cancellationToken);
            
            return imageOnDisk;
        }
        catch (UnknownImageFormatException exception)
        {
            logger.LogError(exception, "Error loading image from disk");
        }

        return null;
    }
}