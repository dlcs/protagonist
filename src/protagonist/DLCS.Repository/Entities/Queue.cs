#nullable disable

namespace DLCS.Repository.Entities;

public partial class Queue
{
    public int Customer { get; set; }
    public int Size { get; set; }
    public string Name { get; set; }
}
