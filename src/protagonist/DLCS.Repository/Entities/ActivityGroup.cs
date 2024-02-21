#nullable disable

using System;

namespace DLCS.Repository.Entities;

public partial class ActivityGroup
{
    public string Group { get; set; }
    public DateTime? Since { get; set; }
    public string Inhabitant { get; set; }
}
