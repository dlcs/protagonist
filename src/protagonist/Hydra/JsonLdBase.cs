using System;
using System.Linq;
using Newtonsoft.Json;

namespace Hydra;

public abstract class JsonLdBase
{
    [JsonProperty(Order = 1, PropertyName = "@context")]
    public virtual string? Context
    {
        get => InternalContext;
        set => InternalContext = value;
    }

    [JsonIgnore]
    protected string? InternalContext;

    [JsonProperty(Order = 2, PropertyName = "@id")]
    public string? Id { get; set; }

    [JsonProperty(Order = 3, PropertyName = "@type")]
    public virtual string Type { get; set; }
    
    public string? GetLastPathElement()
    {
        return Id.GetLastPathElement();
    }

    public int? GetLastPathElementAsInt()
    {
        return Id.GetLastPathElementAsInt();
    }
}