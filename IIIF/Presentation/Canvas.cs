using IIIF.Presentation.Annotation;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public class Canvas : StructureBase, IStructuralLocation, IPaintable // but not ISpatial or ITemporal - that's for content
    {
        public override string Type => nameof(Canvas);

        public int? Width { get; set; }
        public int? Height { get; set; }
        public double? Duration { get; set; }
        public List<AnnotationPage>? Items { get; set; }
    }
}
