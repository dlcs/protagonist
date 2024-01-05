namespace Engine.Ingest.Image.Appetiser;

/// <summary>
/// Response model for receiving error requests back from Appetiser.
/// </summary>
public class AppetiserResponseErrorModel : AppetiserResponse
{
    public AppetiserResponseErrorModel()
    {
        ResponseType = ResponseType.ErrorResponse;
    }
    public string Message { get; set; }
    public string Status { get; set; }
}