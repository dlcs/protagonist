using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DLCS.Model.Assets
{
    public class ThumbnailPolicy
    {
        private List<int> sizeList = null;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Sizes { get; set; }

        public List<int> SizeList
        {
            get
            {
                if (sizeList == null && Sizes != null)
                {
                    sizeList = Sizes.Split(',').Select(int.Parse).ToList();
                }
                return sizeList;
            }
        }
    }
}
