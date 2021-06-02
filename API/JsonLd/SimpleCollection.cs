using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace API.JsonLd
{
    /// <summary>
    /// Base class for JsonLd collections
    /// </summary>
    /// <typeparam name="T">Type of member</typeparam>
    public class SimpleCollection<T> : JsonLdBase
        where T : JsonLdBase
    {
        [JsonProperty("totalItems", Order = 5)]
        public int TotalItems { get; set; }

        [JsonProperty("pageSize", Order = 6)] 
        public int PageSize { get; set; }
        
        [JsonProperty("member", Order = 7)] 
        public IEnumerable<T> Members { get; set; } = Enumerable.Empty<T>();

        protected SimpleCollection()
        {
            Type = "Collection";
        }
    }
}