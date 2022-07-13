#nullable disable

using System.Diagnostics;

namespace DLCS.Model.Assets
{
    [DebuggerDisplay("{Name}")]
    public partial class ImageOptimisationPolicy
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TechnicalDetails { get; set; }
    }
}
