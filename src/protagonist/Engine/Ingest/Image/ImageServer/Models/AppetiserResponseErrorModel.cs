namespace Engine.Ingest.Image.ImageServer.Models;

/// <summary>
/// Response model for receiving error requests back from Appetiser.
/// </summary>
public class AppetiserResponseErrorModel : IImageProcessorResponse
{
    public required string Message { get; init; }
    public required int Status { get; init; }
}
