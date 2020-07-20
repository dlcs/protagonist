using System.Collections.Generic;
using System.Linq;

namespace IIIF.Presentation.Constants
{
    public static class Context
    {
        public const string Presentation3Context = "http://iiif.io/api/presentation/3/context.json";

        public static void AddPresentation3Context(this ResourceBase resource, params string[] additionalContexts)
        {
            if(additionalContexts != null && additionalContexts.Any())
            {
                var listOfContexts = new List<string>(additionalContexts);
                listOfContexts.Append(Presentation3Context);
                resource.Context = listOfContexts;
            }
            else
            {
                resource.Context = Presentation3Context;
            }
        }
    }
}
