using System;
using System.Linq;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace API.JsonLd
{
    /// <summary>
    /// Base class for all JsonLd objects
    /// </summary>
    public class JsonLdBase
    {
        [JsonProperty("@context", Order = 1)]
        public string Context { get; set; } = "http://www.w3.org/ns/hydra/context.jsonld";
        
        [JsonProperty("@id", Order = 2)]
        public string Id { get; set; }
        
        [JsonProperty("@type", Order = 3)]
        public string Type { get; set; }

        public string? GetLastPathElement()
        {
            if (string.IsNullOrEmpty(Id)) return null;

            var parts = Id.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();
            return parts.Count < 2 ? null : parts[^1];
        }

        public int? GetLastPathElementAsInt()
        {
            var last = GetLastPathElement();
            if (string.IsNullOrWhiteSpace(last))
            {
                return null;
            }
            // We want this to throw if not an int
            return int.Parse(last);
        }
    } 
}