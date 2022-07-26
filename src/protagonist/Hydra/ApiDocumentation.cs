using System.Collections.Generic;
using Hydra.Model;
using Newtonsoft.Json;

namespace Hydra
{
    public class ApiDocumentation
    {
        [JsonProperty(Order = 1, PropertyName = "@context")]
        public Dictionary<string, object> Context { get; }

        [JsonProperty(Order = 2, PropertyName = "@id")]
        public string Id { get; private set; }

        [JsonProperty(Order = 3, PropertyName = "@type")]
        public string Type
        {
            get { return "ApiDocumentation"; }
        }

        [JsonProperty(Order = 100, PropertyName = "supportedClass")]
        public Class[] SupportedClasses { get; set; }

        public ApiDocumentation(string vocab, string id, Class[] supportedClasses)
        {
            SupportedClasses = supportedClasses;
            Id = id;
            Context = new Dictionary<string, object>
            {
                {"hydra", "http://www.w3.org/ns/hydra/core#"},
                {"vocab", vocab},
                {"ApiDocumentation", "hydra:ApiDocumentation"},
                {"property", new Link {Id = "hydra:property"}},
                {"readonly", "hydra:readonly"},
                {"writeonly", "hydra:writeonly"},
                {"supportedClass", "hydra:supportedClass"},
                {"supportedProperty", "hydra:supportedProperty"},
                {"supportedOperation", "hydra:supportedOperation"},
                {"method", "hydra:method"},
                {"expects", new Link {Id = "hydra:expects"}},
                {"returns", new Link {Id = "hydra:returns"}},
                {"statusCodes", "hydra:statusCodes"},
                {"code", "hydra:statusCode"},
                {"rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"},
                {"rdfs", "http://www.w3.org/2000/01/rdf-schema#"},
                {"label", "rdfs:label"},
                {"description", "rdfs:comment"},
                {"domain", new Link {Id = "rdfs:domain"}},
                {"range", new Link {Id = "rdfs:range"}},
                {"subClassOf", new Link {Id = "hydra:subClassOf"}}
            };
        }
    }
}
