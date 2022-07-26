using Newtonsoft.Json;

namespace Hydra;

/// <summary>
/// A JSON object with a Hydra context
/// </summary>
public class JsonLdBaseWithHydraContext : JsonLdBase
{
    private const string HydraContext = "http://www.w3.org/ns/hydra/context.jsonld";
    [JsonIgnore]
    public bool WithContext { get; set; }

    public override string? Context
    {
        // Force the HydraContext but if not, don't null any existing context
        get => WithContext ? HydraContext : InternalContext;
        set => InternalContext = value;
    }
}