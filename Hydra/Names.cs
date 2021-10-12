namespace Hydra
{
    public static class Names
    {
        public static string GetNamespacedVersion(string full)
        {
            if (string.IsNullOrWhiteSpace(full))
            {
                return full;
            }
            if (full.StartsWith(Hydra.Base))
            {
                return full.Replace(Hydra.Base, "hydra:");
            }
            if (full.StartsWith(Owl.Base))
            {
                return full.Replace(Owl.Base, "owl:");
            }
            if (full.StartsWith(XmlSchema.Base))
            {
                return full.Replace(XmlSchema.Base, "xsd:");
            }
            return full;
        }

        public static class Hydra
        {
            public const string Base        = "http://www.w3.org/ns/hydra/core#";
            public const string Resource    = "http://www.w3.org/ns/hydra/core#Resource";
            public const string Collection  = "http://www.w3.org/ns/hydra/core#Collection";
        }

        public static class Owl
        {
            public const string Base        = "http://www.w3.org/2002/07/owl#";
            public const string Nothing     = "http://www.w3.org/2002/07/owl#Nothing";
        }

        public static class XmlSchema
        {
            public const string Base                = "http://www.w3.org/2001/XMLSchema#";
            public const string String              = "http://www.w3.org/2001/XMLSchema#string";
            public const string Boolean             = "http://www.w3.org/2001/XMLSchema#boolean";
            public const string DateTime            = "http://www.w3.org/2001/XMLSchema#dateTime";
            public const string Integer             = "http://www.w3.org/2001/XMLSchema#integer";
            public const string NonNegativeInteger  = "http://www.w3.org/2001/XMLSchema#nonNegativeInteger"; 
        }
    }
}