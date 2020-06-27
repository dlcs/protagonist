
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public class AnnotationPage : ResourceBase
    {
        public override string Type => nameof(AnnotationPage);
        public List<IAnnotation>? Items { get; set; }
    }
}
