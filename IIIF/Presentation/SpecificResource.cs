using IIIF.Presentation.Selectors;

namespace IIIF.Presentation
{
    public class SpecificResource : ResourceBase, IStructuralLocation
    {
        public override string Type => nameof(SpecificResource);
        public string Source { get; set; }
        public ISelector Selector { get; set; } 
    }
}
