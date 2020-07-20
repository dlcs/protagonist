using IIIF.Presentation.Services;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public class Manifest : StructureBase, ICollectionItem
    {
        public override string Type => nameof(Manifest);
        public List<Canvas>? Items { get; set; }
        public List<Range>? Structures { get; set; }
        public string? ViewingDirection { get; set; }
        public List<IService>? Services { get; set; }
        public IStructuralLocation? Start { get; set; }
    }
}
