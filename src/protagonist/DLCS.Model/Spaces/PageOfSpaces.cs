using System.Collections.Generic;

namespace DLCS.Model.Spaces;

public class PageOfSpaces
{
    public List<DLCS.Model.Spaces.Space> Spaces { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
}
