using System.Text.Json.Serialization;

namespace Engine.Ingest.Image.ImageServer;

/// <summary>
/// Request model for making requests to Appetiser.
/// </summary>
public class AppetiserRequestModel
{
    public required string ImageId { get; init; }
    public required string JobId { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public required string ThumbDir { get; init; }
    public required string Optimisation { get; init; }
    public required string Operation { get; init; }
    public required string Origin { get; init; }
    
    [JsonPropertyName("thumbIIIFSizes")]
    public required IEnumerable<string> ThumbIIIFSizes  { get; init; }
}
