using System.Collections.Generic;

namespace API.Features.Image
{
    public class PageOfAssets
    {
        public List<DLCS.Model.Assets.Asset> Assets { get; set; }
        public int Page { get; set; }
        public int Total { get; set; }
    }
}