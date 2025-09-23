namespace Engine.Ingest.Image.ImageServer.Models;

/// <summary>
/// Response model for receiving error requests back from Appetiser.
/// </summary>
public class AppetiserResponseErrorModel : IImageProcessorResponse
{
    public string Message { get; set; }
    public string Status { get; set; }
}
