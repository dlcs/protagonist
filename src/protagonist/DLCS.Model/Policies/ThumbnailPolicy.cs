using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DLCS.Model.Policies;

[DebuggerDisplay("{Name}: {Sizes}")]
public class ThumbnailPolicy
{
    private IReadOnlyCollection<int>? sizeList;

    public string Id { get; set; }
    public string Name { get; set; }
    public string Sizes { get; set; }

    /// <summary>
    /// Get a list of available sizes, ordered from largest -> smallest
    /// </summary>
    public IReadOnlyCollection<int> SizeList
    {
        get
        {
            if (sizeList != null) return sizeList;
            
            if (string.IsNullOrEmpty(Sizes))
            {
                sizeList = new List<int>();
            }
            else
            {
                sizeList = Sizes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .OrderByDescending(s => s)
                    .ToList();
            }
            return sizeList;
        }
    }
}
