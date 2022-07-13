using System.Text.Json.Serialization;
using DLCS.Model.Assets;

namespace Engine.Ingest.Models;

/// <summary>
/// Serialized Inversion MessagingEvent passed to the Engine by DLCS API.
/// </summary>
/// <remarks>Legacy fields from the Inversion framework.</remarks>
public class IncomingIngestEvent
{
    private const string AssetDictionaryKey = "image";
        
    /// <summary>
    /// Gets the type of MessagingEvent.
    /// </summary>
    [JsonPropertyName("_type")] 
    public string Type { get; }

    /// <summary>
    /// Gets the date this message was created.
    /// </summary>
    [JsonPropertyName("_created")]
    public DateTime? Created { get; }
        
    /// <summary>
    /// Gets the type of this message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; }
        
    /// <summary>
    /// A collection of additional parameters associated with event. 
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, string> Params { get; }

    /// <summary>
    /// Serialized <see cref="Asset"/> as JSON.
    /// </summary>
    public string? AssetJson => Params.TryGetValue(AssetDictionaryKey, out var image) ? image : null;

    [JsonConstructor]
    public IncomingIngestEvent(string type, DateTime? created, string message, Dictionary<string, string>? @params)
    {
        Type = type;
        Created = created;
        Message = message;
        Params = @params ?? new Dictionary<string, string>();
    }
}