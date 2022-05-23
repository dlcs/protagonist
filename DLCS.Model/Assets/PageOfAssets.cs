using System.Collections.Generic;

namespace DLCS.Model.Assets
{
    public class PageOfAssets
    {
        public List<Asset> Assets { get; set; }
        public int Page { get; set; }
        public int Total { get; set; }
    }
}