using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DLCS.Model.Policies;

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
