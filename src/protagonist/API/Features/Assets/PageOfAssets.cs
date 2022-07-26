using System.Collections.Generic;
using DLCS.Model.Assets;

namespace API.Features.Assets;

public class PageOfAssets
{
    public List<Asset> Assets { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
}