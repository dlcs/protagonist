#nullable disable

namespace DLCS.Model.Assets.CustomHeaders;

public partial class CustomHeader
{
    public string Id { get; set; }
    public int Customer { get; set; }
    public int? Space { get; set; }
    public string Role { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}
