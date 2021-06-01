using System;
using System.Linq;

namespace API.JsonLd
{
    public class ApiKey : JsonLdBase
    {
        /// <summary>
        /// Get the APIKey value from Id. 
        /// </summary>
        public string? Key
        {
            get
            {
                if (string.IsNullOrEmpty(Id)) return null;

                var parts = Id.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();
                return parts.Count < 2 ? null : parts[^1];
            }
        }
        
        /// <summary>
        /// Get or set secret value for this API key.
        /// </summary>
        public string? Secret { get; set; }
    }
}