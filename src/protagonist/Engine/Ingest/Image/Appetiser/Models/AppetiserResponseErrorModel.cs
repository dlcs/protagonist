namespace Engine.Ingest.Image.Appetiser;

/// <summary>
/// Response model for receiving error requests back from Appetiser.
/// </summary>
public class AppetiserResponseErrorModel : IAppetiserResponse
{
    public string Message { get; set; }
    public string Status { get; set; }
}