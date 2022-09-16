using System.Diagnostics;

namespace DLCS.Model.Policies;

[DebuggerDisplay("{Name}")]
public class ImageOptimisationPolicy
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string[] TechnicalDetails { get; set; }
}
