using System;
using System.Linq;
using Newtonsoft.Json;

namespace Hydra
{
    public abstract class JSONLDBase
    {
        [JsonProperty(Order = 1, PropertyName = "@context")]
        public virtual string Context { get; set; }

        [JsonProperty(Order = 2, PropertyName = "@id")]
        public string Id { get; set; }

        [JsonProperty(Order = 3, PropertyName = "@type")]
        public virtual string Type { get; set; }
        
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