using Newtonsoft.Json;

namespace Hydra
{
    /// <summary>
    /// A JSON object with a Hydra context
    /// </summary>
    public class JsonLdBaseWithHydraContext : JsonLdBase
    {
        [JsonIgnore]
        public bool IncludeContext { get; set; }

        public override string Context
        {
            get { return IncludeContext ? "http://www.w3.org/ns/hydra/context.jsonld" : null; }
        }
    }
}