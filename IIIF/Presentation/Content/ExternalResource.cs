using IIIF.Presentation.Annotation;
using System.Collections.Generic;

namespace IIIF.Presentation.Content
{
    public class ExternalResource : ResourceBase, IPaintable
    {
        public ExternalResource(string type)
        {
            Type = type;
        }

        public override string Type { get; }

        /// <summary>
        /// Only content resources may have the Format property
        /// </summary>
        public string? Format { get; set; }
        public List<string>? Language { get; set; }
        public List<AnnotationPage>? Annotations { get; set; }
    }
}
