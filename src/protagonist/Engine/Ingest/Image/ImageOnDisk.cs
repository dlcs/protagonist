namespace Engine.Ingest.Image;

/// <summary>
/// Represents an image that has been saved to disk
/// </summary>
public class ImageOnDisk
{
    public string Path { get; set; } = null!;
    public int Height { get; set; }
    public int Width { get; set; }

    public int MaxDimension => Math.Max(Height, Width);
}
