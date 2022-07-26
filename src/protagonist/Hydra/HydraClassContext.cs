using System.Collections.Generic;
using Newtonsoft.Json;

namespace Hydra;

/// <summary>
/// Starting context for adding your own vocab to a class
/// </summary>
public class HydraClassContext
{
    [JsonProperty(Order = 1, PropertyName = "@context")]
    public Dictionary<string, object> Context
    {
        get { return _context; }
    }

    protected void Add(string key, object value)
    {
        _context.Add(key, value);
    }

    private readonly Dictionary<string, object> _context;

    public HydraClassContext()
    {
        _context = new Dictionary<string, object> {{"hydra", Names.Hydra.Base}};
    }
}