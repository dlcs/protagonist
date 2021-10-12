using System;
using System.Linq;
using System.Reflection;
using DLCS.HydraModel.Settings;
using Hydra;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    public class DlcsClassContext : HydraClassContext
    {
        public DlcsClassContext(HydraSettings settings)
        {
            Add("vocab", settings.Vocab);
        }

        public DlcsClassContext(HydraSettings settings, Type resourceType) : this(settings)
        {
            AddThroughReflection(resourceType);
        }

        public DlcsClassContext(HydraSettings settings, string typeName) : 
            this(settings, Assembly.GetExecutingAssembly().GetType(typeName))
        {
        }

        /// <summary>
        /// Looks for attributes identifying RDF and Hydra Properties that are exposed
        /// through JSON Properties and bootstraps the context.
        /// 
        /// TODO: if there is never anything OTHER than this to be generated through reflection,
        /// then we can dispense with separate context classes altogether.
        /// But leave for now...
        /// </summary>
        /// <param name="resourceType"></param>
        protected void AddThroughReflection(Type resourceType)
        {
            var vocabName = "vocab:" + resourceType.Name;
            
            Add(resourceType.Name, vocabName);
            foreach (var property in resourceType.GetProperties())
            {
                var attrs = property.GetCustomAttributes(true);
                var jsonProp = attrs.OfType<JsonPropertyAttribute>().SingleOrDefault();
                if (jsonProp != null)
                {
                    var rdfProp = attrs.OfType<RdfPropertyAttribute>().SingleOrDefault();
                    if (rdfProp != null)
                    {
                        Add(jsonProp.PropertyName, vocabName + "/" + jsonProp.PropertyName);
                    }
                    var hydraLink = attrs.OfType<HydraLinkAttribute>().SingleOrDefault();
                    if (hydraLink != null)
                    {
                        Add(jsonProp.PropertyName, new Link { Id = vocabName + "/" + jsonProp.PropertyName });
                    }
                }
            }
        }
    }
}
