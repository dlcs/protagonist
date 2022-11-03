#nullable disable

using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public class ImageLocation
{
    public AssetId Id { get; set; }
    public string S3 { get; set; }
    public string Nas { get; set; }
}
