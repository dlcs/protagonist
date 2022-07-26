#nullable disable

namespace DLCS.Repository.Entities;

public partial class EntityCounter
{
    public string Type { get; set; }
    public string Scope { get; set; }
    public long Next { get; set; }
    public int Customer { get; set; }
}
