namespace Engine.Ingest.Image.ImageServer;

/// <summary>
/// Request model for making requests to Appetiser.
/// </summary>
public class AppetiserRequestModel
{
    public string ImageId { get; set; }
    public string JobId { get; set; }
    public string Source { get; set; }
    public string Destination { get; set; }
    public string ThumbDir { get; set; }
    public string Optimisation { get; set; }
    public string Operation { get; set; }
    public string Origin { get; set; }
    public IEnumerable<string> ThumbIIIFSize  { get; set; }
}
