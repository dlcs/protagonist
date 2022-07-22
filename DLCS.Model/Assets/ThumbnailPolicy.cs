using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using IIIF.ImageApi;

namespace DLCS.Model.Assets
{
    [DebuggerDisplay("{Name}: {Sizes}")]
    public class ThumbnailPolicy
    {
        private List<int>? sizeList = null;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Sizes { get; set; }

        public List<int> SizeList
        {
            get
            {
                if (sizeList == null && !string.IsNullOrEmpty(Sizes))
                {
                    sizeList = Sizes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                }
                return sizeList;
            }
        }
    }
}
