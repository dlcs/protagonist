#nullable disable

namespace DLCS.Model.Assets.NamedQueries;

public class NamedQuery
{
    public string Id { get; set; }
    public int Customer { get; set; }
    public string Name { get; set; }
    public bool Global { get; set; }
    public string Template { get; set; }
}
