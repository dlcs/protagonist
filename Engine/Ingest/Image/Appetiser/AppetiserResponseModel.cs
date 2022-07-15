using DLCS.Model.Assets;

namespace Engine.Ingest.Image.Appetiser;

/// <summary>
/// Response model for receiving requests back from Appetiser.
/// </summary>
public class AppetiserResponseModel
{
    public string ImageId { get; set; }
    public string JobId { get; set; }
    public string Optimisation { get; set; }
    public string JP2 { get; set; }
    public string Origin { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public string InfoJson { get; set; }
    public IEnumerable<ImageOnDisk> Thumbs { get; set; }
}