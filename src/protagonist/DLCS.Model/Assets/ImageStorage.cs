#nullable disable

using System;

namespace DLCS.Model.Assets;

public partial class ImageStorage
{
    public string Id { get; set; }
    public int Customer { get; set; }
    public int Space { get; set; }
    public long ThumbnailSize { get; set; }
    public long Size { get; set; }
    public DateTime LastChecked { get; set; }
    public bool CheckingInProgress { get; set; }
}
