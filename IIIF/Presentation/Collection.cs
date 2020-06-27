using IIIF.Presentation.Services;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public class Collection : StructureBase, ICollectionItem
    {
        public override string Type => nameof(Collection);
        public List<ICollectionItem>? Items { get; set; }
        public string? ViewingDirection { get; set; }
        public List<IService>? Services { get; set; }

    }
}
